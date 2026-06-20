using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Dto;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;
using System.Security.Claims;

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

            var now = DateTime.UtcNow;
            var periodEnd = now.AddMonths(1);

            var subscription = new Subscription(landlord.Id, plan.Id, now, periodEnd);

            await _subscriptionRepository.AddAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            var subFromDb = await _subscriptionRepository.GetByIdAsync(subscription.Id);

            var dueDate = now.AddDays(7);
            var invoice = new Invoice(subscription.Id, plan.MonthlyPrice, dueDate);

            _context.Invoices.Add(invoice);

            var evt = new SubscriptionEvent(subscription.Id, "Subscription Created",
                $"Plan {plan.Name} activated. ${plan.MonthlyPrice}/mo.");

            _context.SubscriptionEvents.Add(evt);

            await _unitOfWork.SaveChangesAsync();

            var subDto = MapToDto(subFromDb!);

            return Ok(new ActivateSubscriptionResponse(subDto, plan.MonthlyPrice, dueDate, invoice.Id));
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

        private static SubscriptionDto MapToDto(Subscription s)
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
