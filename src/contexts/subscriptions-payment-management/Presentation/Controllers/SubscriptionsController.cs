using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Dto;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;
using System.Security.Claims;
using Stripe;

using StripeInvoice = Stripe.Invoice;
using StripeSubscription = Stripe.Subscription;
using LocalInvoice = Nexora.Domain.Entities.Invoice;
using LocalSubscription = Nexora.Domain.Entities.Subscription;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/subscriptions")]
    public class SubscriptionsController : ControllerBase
    {
        private readonly NexoraDbContext _context;
        private readonly ILandlordRepository _landlordRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUnitOfWork _unitOfWork;

        public SubscriptionsController(
            NexoraDbContext context,
            ILandlordRepository landlordRepository,
            ISubscriptionRepository subscriptionRepository,
            IUnitOfWork unitOfWork)
        {
            _context = context;
            _landlordRepository = landlordRepository;
            _subscriptionRepository = subscriptionRepository;
            _unitOfWork = unitOfWork;
        }

        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _context.SubscriptionPlans
                .Where(p => p.IsActive)
                .OrderBy(p => p.MonthlyPrice)
                .Select(p => new SubscriptionPlanDto(
                    p.Id,
                    p.Name,
                    p.MonthlyPrice,
                    p.MaxPropertiesLimit,
                    p.UnlimitedProperties
                ))
                .ToListAsync();

            return Ok(plans);
        }

        [Authorize]
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            var subscription = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
            if (subscription == null)
                return Ok(new { subscription = (object?)null, message = "No active subscription." });

            var dto = MapToDto(subscription);
            return Ok(new { subscription = dto });
        }

        [Authorize]
        [HttpPost("")]
        public async Task<IActionResult> Activate([FromBody] ActivateSubscriptionRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var landlord = await _landlordRepository.GetByUserIdAsync(userId);
            if (landlord == null)
                return BadRequest("Landlord profile not found.");

            var plan = await _context.SubscriptionPlans.FindAsync(request.SubscriptionPlanId);
            if (plan == null)
                return BadRequest("Subscription plan not found.");

            var existing = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
            if (existing != null)
                return BadRequest("Landlord already has a subscription.");

            // 1. Stripe Customer Creation/Verification
            if (string.IsNullOrEmpty(landlord.StripeCustomerId))
            {
                var customerService = new CustomerService();
                var email = User.FindFirstValue(ClaimTypes.Email) ?? "landlord@nexora.com";
                var customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Email = email,
                    Name = $"{landlord.FirstName} {landlord.LastName}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "landlord_id", landlord.Id.ToString() }
                    }
                });
                landlord.SetStripeCustomerId(customer.Id);
                await _landlordRepository.UpdateAsync(landlord);
                await _unitOfWork.SaveChangesAsync();
            }

            // 2. Stripe Product & Price dynamic check/creation
            var productService = new ProductService();
            Product product;
            try
            {
                product = await productService.GetAsync($"plan_{plan.Id}");
            }
            catch (StripeException)
            {
                product = await productService.CreateAsync(new ProductCreateOptions
                {
                    Id = $"plan_{plan.Id}",
                    Name = plan.Name,
                    Description = $"Nexora Subscription Plan: {plan.Name}"
                });
            }

            var priceService = new PriceService();
            var prices = await priceService.ListAsync(new PriceListOptions
            {
                Product = product.Id,
                Active = true
            });
            var price = prices.FirstOrDefault(p => p.UnitAmount == (long)(plan.MonthlyPrice * 100));
            if (price == null)
            {
                price = await priceService.CreateAsync(new PriceCreateOptions
                {
                    Product = product.Id,
                    UnitAmount = (long)(plan.MonthlyPrice * 100),
                    Currency = "usd",
                    Recurring = new PriceRecurringOptions
                    {
                        Interval = "month"
                    }
                });
            }

            // 3. Stripe Subscription creation (incomplete, waiting for card confirmation)
            // 3. Stripe Subscription creation (incomplete, waiting for card confirmation)
            var stripeSubscriptionService = new Stripe.SubscriptionService();
            StripeSubscription stripeSubscription = await stripeSubscriptionService.CreateAsync(new Stripe.SubscriptionCreateOptions
            {
                Customer = landlord.StripeCustomerId,
                Items = new List<Stripe.SubscriptionItemOptions>
                {
                    new Stripe.SubscriptionItemOptions
                    {
                        Price = price.Id
                    }
                },
                PaymentBehavior = "default_incomplete",
                PaymentSettings = new Stripe.SubscriptionPaymentSettingsOptions
                {
                    SaveDefaultPaymentMethod = "on_subscription"
                },
                Expand = new List<string> { "latest_invoice.payment_intent" }
            });

            StripeInvoice stripeInvoice = stripeSubscription.LatestInvoice;
            var clientSecret = stripeInvoice.ConfirmationSecret?.ClientSecret;

            // 4. Local Database Creation
            var now = DateTime.UtcNow;
            var periodEnd = now.AddMonths(1);

            var subscription = new LocalSubscription(landlord.Id, plan.Id, now, periodEnd);
            subscription.SetStripeSubscriptionId(stripeSubscription.Id);

            await _subscriptionRepository.AddAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            var subFromDb = await _subscriptionRepository.GetByIdAsync(subscription.Id);

            var dueDate = now.AddDays(7);
            var invoice = new LocalInvoice(subscription.Id, plan.MonthlyPrice, dueDate);

            _context.Invoices.Add(invoice);

            var evt = new SubscriptionEvent(subscription.Id, "Subscription Created",
                $"Plan {plan.Name} activated. ${plan.MonthlyPrice}/mo. Stripe Subscription: {stripeSubscription.Id}");

            _context.SubscriptionEvents.Add(evt);

            await _unitOfWork.SaveChangesAsync();

            var subDto = MapToDto(subFromDb!);

            return Ok(new ActivateSubscriptionResponse(subDto, plan.MonthlyPrice, dueDate, invoice.Id, clientSecret));
        }

        [Authorize]
        [HttpGet("payment-methods")]
        public async Task<IActionResult> GetPaymentMethods()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            var savedCards = await _context.SavedCards
                .Where(c => c.LandlordId == landlord.Id)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new PaymentMethodDto(
                    c.Id,
                    c.Brand,
                    c.LastFour,
                    c.ExpiryMonth,
                    c.ExpiryYear,
                    c.HolderName,
                    c.Cvv
                ))
                .ToListAsync();

            return Ok(new { paymentMethods = savedCards });
        }

        [Authorize]
        [HttpGet("payment-methods/{id:long}")]
        public async Task<IActionResult> GetPaymentMethod(long id)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            var card = await _context.SavedCards
                .FirstOrDefaultAsync(c => c.Id == id && c.LandlordId == landlord.Id);

            if (card == null) return NotFound();

            return Ok(new PaymentMethodDetailDto(
                card.Id,
                card.Brand,
                card.LastFour,
                card.FullNumber,
                card.ExpiryMonth,
                card.ExpiryYear,
                card.HolderName,
                card.Cvv
            ));
        }

        [Authorize]
        [HttpPut("payment-methods/{id:long}")]
        public async Task<IActionResult> UpdatePaymentMethod(long id, [FromBody] UpdatePaymentMethodRequest request)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            var card = await _context.SavedCards
                .FirstOrDefaultAsync(c => c.Id == id && c.LandlordId == landlord.Id);

            if (card == null) return NotFound();

            card.Update(
                request.Brand,
                request.FullNumber,
                request.ExpiryMonth,
                request.ExpiryYear,
                request.HolderName,
                request.Cvv
            );

            await _context.SaveChangesAsync();

            return Ok(new PaymentMethodDetailDto(
                card.Id,
                card.Brand,
                card.LastFour,
                card.FullNumber,
                card.ExpiryMonth,
                card.ExpiryYear,
                card.HolderName,
                card.Cvv
            ));
        }

        [Authorize]
        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            var subscription = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
            if (subscription == null)
                return Ok(new { invoices = Array.Empty<InvoiceDto>() });

            var invoices = await _context.Invoices
                .Where(i => i.SubscriptionId == subscription.Id)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new InvoiceDto(
                    i.Id,
                    i.Amount,
                    i.Status.ToString(),
                    i.DueDate,
                    i.CreatedAt
                ))
                .ToListAsync();

            return Ok(new { invoices });
        }

        [Authorize]
        [HttpPut("status")]
        public async Task<IActionResult> Cancel()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            var subscription = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
            if (subscription == null)
                return BadRequest("No active subscription found.");

            if (subscription.Status != SubscriptionStatus.Active && subscription.Status != SubscriptionStatus.PastDue)
                return BadRequest($"Cannot cancel subscription in status {subscription.Status}.");

            subscription.Cancel();

            var evt = new SubscriptionEvent(subscription.Id, "Subscription Cancelled",
                $"Cancelled at period end: {subscription.CurrentPeriodEnd:yyyy-MM-dd}.");

            _context.SubscriptionEvents.Add(evt);
            await _subscriptionRepository.UpdateAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new
            {
                message = "Subscription will be cancelled at period end.",
                currentPeriodEnd = subscription.CurrentPeriodEnd
            });
        }

        private async Task<Landlord?> GetLandlordAsync()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return null;
            return await _landlordRepository.GetByUserIdAsync(userId);
        }

        private static SubscriptionDto MapToDto(Nexora.Domain.Entities.Subscription s)
        {
            return new SubscriptionDto(
                s.Id,
                new SubscriptionPlanDto(
                    s.Plan.Id,
                    s.Plan.Name,
                    s.Plan.MonthlyPrice,
                    s.Plan.MaxPropertiesLimit,
                    s.Plan.UnlimitedProperties
                ),
                s.Status.ToString(),
                s.StartedAt,
                s.CurrentPeriodStart,
                s.CurrentPeriodEnd,
                s.CancelAtPeriodEnd
            );
        }
    }
}
