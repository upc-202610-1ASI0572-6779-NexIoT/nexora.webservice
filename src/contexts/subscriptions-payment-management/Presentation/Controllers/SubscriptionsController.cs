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

            if (string.IsNullOrEmpty(Stripe.StripeConfiguration.ApiKey) || Stripe.StripeConfiguration.ApiKey.Contains("YOUR_STRIPE"))
            {
                var existingLocal = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
                if (existingLocal != null)
                {
                    if (existingLocal.Status == SubscriptionStatus.Cancelled || existingLocal.Status == SubscriptionStatus.Expired)
                    {
                        var oldInvs = await _context.Invoices.Where(i => i.SubscriptionId == existingLocal.Id).ToListAsync();
                        var oldInvIds = oldInvs.Select(i => i.Id).ToList();
                        var oldPmts = await _context.Payments.Where(p => oldInvIds.Contains(p.InvoiceId)).ToListAsync();
                        var oldEvts = await _context.SubscriptionEvents.Where(e => e.SubscriptionId == existingLocal.Id).ToListAsync();

                        _context.Payments.RemoveRange(oldPmts);
                        _context.Invoices.RemoveRange(oldInvs);
                        _context.SubscriptionEvents.RemoveRange(oldEvts);
                        _context.Subscriptions.Remove(existingLocal);

                        await _unitOfWork.SaveChangesAsync();
                    }
                    else
                    {
                        if (existingLocal.SubscriptionPlanId == plan.Id)
                        {
                            return BadRequest("Already subscribed to this plan.");
                        }

                        existingLocal.ChangePlan(plan.Id, DateTime.UtcNow.AddMonths(1));
                        await _subscriptionRepository.UpdateAsync(existingLocal);

                        var changePlanEvt = new SubscriptionEvent(
                            existingLocal.Id,
                            "Plan Changed",
                            $"Upgraded/Downgraded plan to: {plan.Name} (Local Dev). Price: ${plan.MonthlyPrice}/mo."
                        );
                        _context.SubscriptionEvents.Add(changePlanEvt);
                        await _unitOfWork.SaveChangesAsync();

                        var subDtoChanged = MapToDto(existingLocal);
                        return Ok(new ActivateSubscriptionResponse(subDtoChanged, plan.MonthlyPrice, DateTime.UtcNow.AddDays(7), 0, null));
                    }
                }

                var localNow = DateTime.UtcNow;
                var localPeriodEnd = localNow.AddMonths(1);
                var localSub = new Nexora.Domain.Entities.Subscription(landlord.Id, plan.Id, localNow, localPeriodEnd);
                await _subscriptionRepository.AddAsync(localSub);
                await _unitOfWork.SaveChangesAsync();

                var localSubFromDb = await _subscriptionRepository.GetByIdAsync(localSub.Id);
                var localDueDate = localNow.AddDays(7);
                var localInvoice = new Nexora.Domain.Entities.Invoice(localSub.Id, plan.MonthlyPrice, localDueDate);
                
                // For local dev, mark the mock payment as Paid immediately
                localInvoice.MarkAsPaid();
                _context.Invoices.Add(localInvoice);
                await _unitOfWork.SaveChangesAsync(); // Generates localInvoice.Id

                var localPayment = new Payment(localInvoice.Id, localInvoice.Amount, "local", "local_tx_" + Guid.NewGuid());
                localPayment.Succeed();
                _context.Payments.Add(localPayment);

                // Save or update card details locally
                if (!string.IsNullOrEmpty(request.FullNumber))
                {
                    var existingCard = await _context.SavedCards
                        .FirstOrDefaultAsync(c => c.LandlordId == landlord.Id);
                    if (existingCard != null)
                    {
                        existingCard.Update(
                            request.Brand,
                            "************" + (request.FullNumber.Length >= 4 ? request.FullNumber[^4..] : request.FullNumber),
                            request.ExpiryMonth,
                            request.ExpiryYear,
                            request.HolderName,
                            "***"
                        );
                    }
                    else
                    {
                        var card = new SavedCard(
                            landlord.Id,
                            request.Brand ?? "Visa",
                            "************" + (request.FullNumber.Length >= 4 ? request.FullNumber[^4..] : request.FullNumber),
                            request.ExpiryMonth ?? "12",
                            request.ExpiryYear ?? "29",
                            request.HolderName ?? "Cardholder User",
                            "***"
                        );
                        _context.SavedCards.Add(card);
                    }
                }

                var localEvt = new SubscriptionEvent(localSub.Id, "Subscription Created",
                    $"Plan {plan.Name} activated locally. ${plan.MonthlyPrice}/mo.");
                _context.SubscriptionEvents.Add(localEvt);
                await _unitOfWork.SaveChangesAsync();

                var localSubDto = MapToDto(localSubFromDb!);
                return Ok(new ActivateSubscriptionResponse(localSubDto, plan.MonthlyPrice, localDueDate, localInvoice.Id, null));
            }
            var existing = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
            if (existing != null)
            {
                if (existing.Status == SubscriptionStatus.Cancelled || existing.Status == SubscriptionStatus.Expired)
                {
                    // Clean up and remove old inactive subscription from database
                    var oldInvs = await _context.Invoices.Where(i => i.SubscriptionId == existing.Id).ToListAsync();
                    var oldInvIds = oldInvs.Select(i => i.Id).ToList();
                    var oldPmts = await _context.Payments.Where(p => oldInvIds.Contains(p.InvoiceId)).ToListAsync();
                    var oldEvts = await _context.SubscriptionEvents.Where(e => e.SubscriptionId == existing.Id).ToListAsync();

                    _context.Payments.RemoveRange(oldPmts);
                    _context.Invoices.RemoveRange(oldInvs);
                    _context.SubscriptionEvents.RemoveRange(oldEvts);
                    _context.Subscriptions.Remove(existing);

                    await _unitOfWork.SaveChangesAsync();
                }
                else
                {
                    // Upgrade/Downgrade Plan Change Flow
                    if (existing.SubscriptionPlanId == plan.Id)
                    {
                        return BadRequest("Already subscribed to this plan.");
                    }

                    // 1. Stripe Product & Price dynamic check/creation
                    var stripeProductService = new ProductService();
                    Product stripeProduct;
                    try
                    {
                        stripeProduct = await stripeProductService.GetAsync($"plan_{plan.Id}");
                    }
                    catch (StripeException)
                    {
                        stripeProduct = await stripeProductService.CreateAsync(new ProductCreateOptions
                        {
                            Id = $"plan_{plan.Id}",
                            Name = plan.Name,
                            Description = $"Nexora Subscription Plan: {plan.Name}"
                        });
                    }

                    var stripePriceService = new PriceService();
                    var stripePrices = await stripePriceService.ListAsync(new PriceListOptions
                    {
                        Product = stripeProduct.Id,
                        Active = true
                    });
                    var stripePrice = stripePrices.FirstOrDefault(p => p.UnitAmount == (long)(plan.MonthlyPrice * 100));
                    if (stripePrice == null)
                    {
                        stripePrice = await stripePriceService.CreateAsync(new PriceCreateOptions
                        {
                            Product = stripeProduct.Id,
                            UnitAmount = (long)(plan.MonthlyPrice * 100),
                            Currency = "usd",
                            Recurring = new PriceRecurringOptions { Interval = "month" }
                        });
                    }

                    // 2. Update Stripe Subscription
                    var stripeSubSvc = new Stripe.SubscriptionService();
                    var stripeSub = await stripeSubSvc.GetAsync(existing.StripeSubscriptionId);
                    var subscriptionItem = stripeSub.Items.Data.First();

                    var stripeSubscriptionUpdateOptions = new Stripe.SubscriptionUpdateOptions
                    {
                        Items = new List<Stripe.SubscriptionItemOptions>
                        {
                            new Stripe.SubscriptionItemOptions
                            {
                                Id = subscriptionItem.Id,
                                Price = stripePrice.Id
                            }
                        },
                        ProrationBehavior = "create_prorations"
                    };
                    stripeSubscriptionUpdateOptions.AddExpand("latest_invoice.confirmation_secret");

                    stripeSub = await stripeSubSvc.UpdateAsync(existing.StripeSubscriptionId, stripeSubscriptionUpdateOptions);

                    // 3. Local database update
                    existing.ChangePlan(plan.Id, DateTime.UtcNow.AddMonths(1));
                    await _subscriptionRepository.UpdateAsync(existing);

                    var changePlanEvt = new SubscriptionEvent(
                        existing.Id,
                        "Plan Changed",
                        $"Upgraded/Downgraded plan to: {plan.Name}. Price: ${plan.MonthlyPrice}/mo."
                    );
                    _context.SubscriptionEvents.Add(changePlanEvt);

                    await _unitOfWork.SaveChangesAsync();

                    var prorationClientSecret = stripeSub.LatestInvoice?.ConfirmationSecret?.ClientSecret;

                    return Ok(new
                    {
                        subscription = MapToDto(existing),
                        clientSecret = prorationClientSecret
                    });
                }
            }

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

            // Create and attach payment method in Stripe if card details are provided
            string? pmId = null;
            if (!string.IsNullOrEmpty(request.FullNumber))
            {
                try
                {
                    var paymentMethodService = new PaymentMethodService();
                    var pm = await paymentMethodService.CreateAsync(new PaymentMethodCreateOptions
                    {
                        Type = "card",
                        Card = new PaymentMethodCardOptions
                        {
                            Number = request.FullNumber,
                            ExpMonth = long.Parse(request.ExpiryMonth!),
                            ExpYear = long.Parse(request.ExpiryYear!.Length == 2 ? "20" + request.ExpiryYear : request.ExpiryYear),
                            Cvc = request.Cvv
                        },
                        BillingDetails = new PaymentMethodBillingDetailsOptions
                        {
                            Name = request.HolderName
                        }
                    });

                    await paymentMethodService.AttachAsync(pm.Id, new PaymentMethodAttachOptions
                    {
                        Customer = landlord.StripeCustomerId
                    });

                    var customerService = new CustomerService();
                    await customerService.UpdateAsync(landlord.StripeCustomerId, new CustomerUpdateOptions
                    {
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = pm.Id
                        }
                    });

                    pmId = pm.Id;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Stripe PaymentMethod creation failed: {ex.Message}");
                }
            }

            // 3. Stripe Subscription creation (incomplete, waiting for card confirmation)
            var stripeSubscriptionService = new Stripe.SubscriptionService();
            var subOptions = new Stripe.SubscriptionCreateOptions
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
                Expand = new List<string> { "latest_invoice.confirmation_secret", "latest_invoice.payments.data.payment" }
            };
            if (!string.IsNullOrEmpty(pmId))
            {
                subOptions.DefaultPaymentMethod = pmId;
            }
            StripeSubscription stripeSubscription = await stripeSubscriptionService.CreateAsync(subOptions);

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

            // Mark it as Paid locally immediately for a seamless checkout experience
            invoice.MarkAsPaid();
            _context.Invoices.Add(invoice);
            await _unitOfWork.SaveChangesAsync(); // Generates invoice.Id

            var transactionId = stripeInvoice?.Id ?? "stripe_tx";
            var payment = new Payment(invoice.Id, invoice.Amount, "stripe", transactionId);
            payment.Succeed();
            _context.Payments.Add(payment);

            // Save or update card details locally
            if (!string.IsNullOrEmpty(request.FullNumber))
            {
                var existingCard = await _context.SavedCards
                    .FirstOrDefaultAsync(c => c.LandlordId == landlord.Id);
                if (existingCard != null)
                {
                    existingCard.Update(
                        request.Brand,
                        "************" + (request.FullNumber.Length >= 4 ? request.FullNumber[^4..] : request.FullNumber),
                        request.ExpiryMonth,
                        request.ExpiryYear,
                        request.HolderName,
                        "***"
                    );
                }
                else
                {
                    var card = new SavedCard(
                        landlord.Id,
                        request.Brand ?? "Visa",
                        "************" + (request.FullNumber.Length >= 4 ? request.FullNumber[^4..] : request.FullNumber),
                        request.ExpiryMonth ?? "12",
                        request.ExpiryYear ?? "29",
                        request.HolderName ?? "Cardholder User",
                        "***"
                    );
                    _context.SavedCards.Add(card);
                }
            }

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
        [HttpPost("payment-methods")]
        public async Task<IActionResult> CreatePaymentMethod([FromBody] UpdatePaymentMethodRequest request)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            // If a card already exists, update it instead of creating duplicates
            var existingCard = await _context.SavedCards
                .FirstOrDefaultAsync(c => c.LandlordId == landlord.Id);

            if (existingCard != null)
            {
                existingCard.Update(
                    request.Brand,
                    request.FullNumber,
                    request.ExpiryMonth,
                    request.ExpiryYear,
                    request.HolderName,
                    request.Cvv
                );
                await _context.SaveChangesAsync();
                return Ok(new PaymentMethodDetailDto(
                    existingCard.Id,
                    existingCard.Brand,
                    existingCard.LastFour,
                    existingCard.FullNumber,
                    existingCard.ExpiryMonth,
                    existingCard.ExpiryYear,
                    existingCard.HolderName,
                    existingCard.Cvv
                ));
            }

            var card = new SavedCard(
                landlord.Id,
                request.Brand,
                request.FullNumber,
                request.ExpiryMonth,
                request.ExpiryYear,
                request.HolderName,
                request.Cvv
            );

            _context.SavedCards.Add(card);
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
