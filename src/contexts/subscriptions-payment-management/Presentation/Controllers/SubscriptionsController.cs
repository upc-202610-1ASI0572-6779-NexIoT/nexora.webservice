using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Dto;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

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
        // Restricts the PaymentSheet to card only, matching the web app's checkout
        // (which uses Stripe's basic Card Element). Hides Klarna, Cash App Pay,
        // Amazon Pay, Bank, Link, etc. that Stripe would otherwise offer automatically
        // based on the dashboard's enabled payment methods.
        private static readonly List<string> AllowedPaymentMethodTypes = new() { "card" };

        private readonly NexoraDbContext _context;
        private readonly ILandlordRepository _landlordRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _config;

        public SubscriptionsController(
            NexoraDbContext context,
            ILandlordRepository landlordRepository,
            ISubscriptionRepository subscriptionRepository,
            IUnitOfWork unitOfWork,
            IConfiguration config)
        {
            _context = context;
            _landlordRepository = landlordRepository;
            _subscriptionRepository = subscriptionRepository;
            _unitOfWork = unitOfWork;
            _config = config;
        }

        /// <summary>
        /// Public Stripe configuration (publishable key) needed to initialise the
        /// flutter_stripe SDK client-side. Safe to expose without authentication.
        /// </summary>
        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            var key = _config["Stripe:PublishableKey"] ?? string.Empty;
            return Ok(new StripeConfigDto(key));
        }

        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _context.SubscriptionPlans
                .Where(p => p.IsActive)
                .OrderBy(p => p.MonthlyPrice)
                .ToListAsync();

            var dtos = plans.Select(BuildPlanDto).ToList();
            return Ok(dtos);
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
                        return Ok(new { subscription = subDtoChanged, clientSecret = (string?)null });
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
                _context.Invoices.Add(localInvoice);

                var localEvt = new SubscriptionEvent(localSub.Id, "Subscription Created",
                    $"Plan {plan.Name} activated locally. ${plan.MonthlyPrice}/mo.");
                _context.SubscriptionEvents.Add(localEvt);
                await _unitOfWork.SaveChangesAsync();

                var localSubDto = MapToDto(localSubFromDb!);
                return Ok(new
                {
                    subscription = localSubDto,
                    clientSecret = (string?)null
                });
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
                        ProrationBehavior = "create_prorations",
                        PaymentSettings = new Stripe.SubscriptionPaymentSettingsOptions
                        {
                            PaymentMethodTypes = AllowedPaymentMethodTypes
                        }
                    };
                    stripeSubscriptionUpdateOptions.AddExpand("latest_invoice.confirmation_secret");
                    stripeSubscriptionUpdateOptions.AddExpand("latest_invoice.payments.data.payment");

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

                    // Downgrades (or fully-credited prorations) can leave the invoice's
                    // PaymentIntent already settled with nothing left to collect; only
                    // surface a clientSecret when the client actually needs to confirm it.
                    var prorationClientSecret = await GetClientSecretIfPaymentRequiredAsync(stripeSub.LatestInvoice);

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
                    SaveDefaultPaymentMethod = "on_subscription",
                    PaymentMethodTypes = AllowedPaymentMethodTypes
                },
                Expand = new List<string> { "latest_invoice.confirmation_secret", "latest_invoice.payments.data.payment" }
            });

            StripeInvoice stripeInvoice = stripeSubscription.LatestInvoice;
            var clientSecret = await GetClientSecretIfPaymentRequiredAsync(stripeInvoice);

            // 4. Local Database Creation
            var now = DateTime.UtcNow;
            var periodEnd = now.AddMonths(1);

            var subscription = new LocalSubscription(landlord.Id, plan.Id, now, periodEnd);

            await _subscriptionRepository.AddAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            var subFromDb = await _subscriptionRepository.GetByIdAsync(subscription.Id);

            var dueDate = now.AddDays(7);
            var invoice = new LocalInvoice(subscription.Id, plan.MonthlyPrice, dueDate);

            _context.Invoices.Add(invoice);

            var evt = new SubscriptionEvent(subscription.Id, "Subscription Created",
                $"Plan {plan.Name} activated. ${plan.MonthlyPrice}/mo.");

            _context.SubscriptionEvents.Add(evt);

            await _unitOfWork.SaveChangesAsync();

            var subDto = MapToDto(subFromDb!);

            return Ok(new ActivateSubscriptionResponse(subDto, plan.MonthlyPrice, dueDate, invoice.Id, clientSecret));
        }

        /// <summary>
        /// Creates a Stripe Checkout Session (hosted payment page) for the selected plan.
        /// The mobile app opens the returned URL in the browser; after payment it calls
        /// POST /sync to reflect the result locally.
        /// </summary>
        [Authorize]
        [HttpPost("checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] ActivateSubscriptionRequest request)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            var plan = await _context.SubscriptionPlans.FindAsync(request.SubscriptionPlanId);
            if (plan == null) return BadRequest("Subscription plan not found.");

            try
            {
                // Ensure Stripe customer for this landlord.
                if (string.IsNullOrEmpty(landlord.StripeCustomerId))
                {
                    var customerService = new CustomerService();
                    var email = User.FindFirstValue(ClaimTypes.Email) ?? "landlord@nexora.com";
                    var customer = await customerService.CreateAsync(new CustomerCreateOptions
                    {
                        Email = email,
                        Name = $"{landlord.FirstName} {landlord.LastName}",
                        Metadata = new Dictionary<string, string> { { "landlord_id", landlord.Id.ToString() } }
                    });
                    landlord.SetStripeCustomerId(customer.Id);
                    await _landlordRepository.UpdateAsync(landlord);
                    await _unitOfWork.SaveChangesAsync();
                }

                var product = await EnsureStripeProductAsync(plan);
                var price = await EnsureStripePriceAsync(product, plan);

                var successUrl = _config["Stripe:CheckoutSuccessUrl"] ?? "https://checkout.stripe.com/success";
                var cancelUrl = _config["Stripe:CheckoutCancelUrl"] ?? "https://checkout.stripe.com/cancel";

                var sessionService = new SessionService();
                var session = await sessionService.CreateAsync(new SessionCreateOptions
                {
                    Mode = "subscription",
                    Customer = landlord.StripeCustomerId,
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions { Price = price.Id, Quantity = 1 }
                    },
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl,
                    SubscriptionData = new SessionSubscriptionDataOptions
                    {
                        Metadata = new Dictionary<string, string>
                        {
                            { "landlord_id", landlord.Id.ToString() },
                            { "plan_id", plan.Id.ToString() }
                        }
                    }
                });

                return Ok(new CheckoutSessionResponse(session.Url, session.Id));
            }
            catch (StripeException ex)
            {
                return StatusCode(502, new { message = $"Stripe error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Reconciles the local subscription with the landlord's active Stripe subscription
        /// (called by the app after returning from Stripe Checkout).
        /// </summary>
        [Authorize]
        [HttpPost("sync")]
        public async Task<IActionResult> Sync()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();
            if (string.IsNullOrEmpty(landlord.StripeCustomerId))
                return Ok(new { subscription = (object?)null });

            try
            {
                var subSvc = new Stripe.SubscriptionService();
                var stripeSubs = await subSvc.ListAsync(new Stripe.SubscriptionListOptions
                {
                    Customer = landlord.StripeCustomerId,
                    Status = "active",
                    Limit = 1
                });
                var stripeSub = stripeSubs.FirstOrDefault();
                if (stripeSub == null)
                {
                    var currentLocal = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
                    return Ok(new { subscription = currentLocal == null ? null : MapToDto(currentLocal) });
                }

                long planId = 0;
                if (stripeSub.Metadata != null && stripeSub.Metadata.TryGetValue("plan_id", out var pidStr))
                    long.TryParse(pidStr, out planId);
                var plan = planId > 0 ? await _context.SubscriptionPlans.FindAsync(planId) : null;

                var existing = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
                var now = DateTime.UtcNow;
                var periodEnd = now.AddMonths(1);

                if (plan != null)
                {
                    if (existing == null)
                    {
                        var newSub = new LocalSubscription(landlord.Id, plan.Id, now, periodEnd);
                        newSub.SetStripeSubscriptionId(stripeSub.Id);
                        await _subscriptionRepository.AddAsync(newSub);
                        await _unitOfWork.SaveChangesAsync();

                        _context.SubscriptionEvents.Add(new SubscriptionEvent(newSub.Id, "Subscription Created",
                            $"Plan {plan.Name} activated via Stripe Checkout. ${plan.MonthlyPrice}/mo."));
                        _context.Invoices.Add(new LocalInvoice(newSub.Id, plan.MonthlyPrice, now.AddDays(30)));
                        await _unitOfWork.SaveChangesAsync();
                        existing = newSub;
                    }
                    else if (existing.SubscriptionPlanId != plan.Id || existing.StripeSubscriptionId != stripeSub.Id)
                    {
                        existing.ChangePlan(plan.Id, periodEnd);
                        existing.SetStripeSubscriptionId(stripeSub.Id);
                        await _subscriptionRepository.UpdateAsync(existing);
                        _context.SubscriptionEvents.Add(new SubscriptionEvent(existing.Id, "Plan Changed",
                            $"Synced from Stripe Checkout to plan {plan.Name}. ${plan.MonthlyPrice}/mo."));
                        await _unitOfWork.SaveChangesAsync();
                    }
                }

                var full = existing == null ? null : await _subscriptionRepository.GetByIdAsync(existing.Id);
                return Ok(new { subscription = full == null ? null : MapToDto(full) });
            }
            catch (StripeException ex)
            {
                return StatusCode(502, new { message = $"Stripe error: {ex.Message}" });
            }
        }

        private static async Task<Product> EnsureStripeProductAsync(SubscriptionPlan plan)
        {
            var productService = new ProductService();
            try
            {
                return await productService.GetAsync($"plan_{plan.Id}");
            }
            catch (StripeException)
            {
                return await productService.CreateAsync(new ProductCreateOptions
                {
                    Id = $"plan_{plan.Id}",
                    Name = plan.Name,
                    Description = $"Nexora Subscription Plan: {plan.Name}"
                });
            }
        }

        private static async Task<Price> EnsureStripePriceAsync(Product product, SubscriptionPlan plan)
        {
            var priceService = new PriceService();
            var prices = await priceService.ListAsync(new PriceListOptions { Product = product.Id, Active = true });
            var price = prices.FirstOrDefault(p => p.UnitAmount == (long)(plan.MonthlyPrice * 100));
            return price ?? await priceService.CreateAsync(new PriceCreateOptions
            {
                Product = product.Id,
                UnitAmount = (long)(plan.MonthlyPrice * 100),
                Currency = "usd",
                Recurring = new PriceRecurringOptions { Interval = "month" }
            });
        }

        /// <summary>
        /// Returns the invoice's PaymentIntent client secret only when the client still
        /// needs to confirm it (e.g. requires a payment method or 3D Secure). Downgrades
        /// or fully-credited prorations can leave the PaymentIntent already 'succeeded'
        /// (or absent, for a zero-amount invoice) — in those cases no native PaymentSheet
        /// confirmation is needed, so we return null.
        /// </summary>
        private static async Task<string?> GetClientSecretIfPaymentRequiredAsync(StripeInvoice? invoice)
        {
            var clientSecret = invoice?.ConfirmationSecret?.ClientSecret;
            if (string.IsNullOrEmpty(clientSecret)) return null;

            // The PaymentIntent id lives under the newer Invoice Payments API shape:
            // invoice.payments.data[].payment.payment_intent_id (Stripe caps expand at
            // 4 levels, so we fetch the PaymentIntent itself in a second call).
            var paymentIntentId = invoice?.Payments?.Data?.FirstOrDefault()?.Payment?.PaymentIntentId;
            if (string.IsNullOrEmpty(paymentIntentId))
            {
                // Status unknown (e.g. no payment record yet) — fail open and let the
                // client attempt confirmation, matching the previous behaviour.
                return clientSecret;
            }

            var paymentIntent = await new PaymentIntentService().GetAsync(paymentIntentId);
            var needsConfirmation = paymentIntent.Status is "requires_payment_method"
                or "requires_confirmation"
                or "requires_action";
            return needsConfirmation ? clientSecret : null;
        }

        [Authorize]
        [HttpGet("payment-method")]
        public async Task<IActionResult> GetPaymentMethod()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            var savedCard = await _context.SavedCards
                .FirstOrDefaultAsync(c => c.LandlordId == landlord.Id);

            if (savedCard == null)
                return Ok(new { paymentMethod = (object?)null });

            var dto = new PaymentMethodDto(
                savedCard.Id,
                savedCard.Brand,
                savedCard.LastFour,
                savedCard.FullNumber,
                savedCard.ExpiryMonth,
                savedCard.ExpiryYear,
                savedCard.HolderName,
                savedCard.Cvv,
                landlord.FirstName,
                landlord.LastName
            );

            return Ok(new { paymentMethod = dto });
        }

        [Authorize]
        [HttpGet("payment-methods")]
        public async Task<IActionResult> GetPaymentMethods()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            var firstName = landlord.FirstName;
            var lastName = landlord.LastName;
            var savedCards = await _context.SavedCards
                .Where(c => c.LandlordId == landlord.Id)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new PaymentMethodDto(
                    c.Id,
                    c.Brand,
                    c.LastFour,
                    c.FullNumber,
                    c.ExpiryMonth,
                    c.ExpiryYear,
                    c.HolderName,
                    c.Cvv,
                    firstName,
                    lastName
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

            if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            {
                try
                {
                    var stripeSubSvc = new Stripe.SubscriptionService();
                    await stripeSubSvc.UpdateAsync(subscription.StripeSubscriptionId, new Stripe.SubscriptionUpdateOptions
                    {
                        CancelAtPeriodEnd = true
                    });
                }
                catch (StripeException ex)
                {
                    return StatusCode(502, new { message = $"Stripe error: {ex.Message}" });
                }
            }

            subscription.Cancel();

            var evt = new SubscriptionEvent(subscription.Id, "Subscription Cancelled",
                $"Cancelled at period end: {subscription.CurrentPeriodEnd:yyyy-MM-dd}.");

            _context.SubscriptionEvents.Add(evt);
            await _subscriptionRepository.UpdateAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            var updated = await _subscriptionRepository.GetByIdAsync(subscription.Id);
            return Ok(new
            {
                message = "Subscription will be cancelled at period end.",
                subscription = MapToDto(updated!)
            });
        }

        /// <summary>Undoes a pending cancellation so the subscription keeps renewing.</summary>
        [Authorize]
        [HttpPut("status/resume")]
        public async Task<IActionResult> Resume()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized();

            var subscription = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
            if (subscription == null)
                return BadRequest("No active subscription found.");

            if (!subscription.CancelAtPeriodEnd)
                return BadRequest("Subscription is not scheduled for cancellation.");

            if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            {
                try
                {
                    var stripeSubSvc = new Stripe.SubscriptionService();
                    await stripeSubSvc.UpdateAsync(subscription.StripeSubscriptionId, new Stripe.SubscriptionUpdateOptions
                    {
                        CancelAtPeriodEnd = false
                    });
                }
                catch (StripeException ex)
                {
                    return StatusCode(502, new { message = $"Stripe error: {ex.Message}" });
                }
            }

            subscription.UndoCancel();

            var evt = new SubscriptionEvent(subscription.Id, "Subscription Resumed",
                "Cancellation undone; subscription will continue renewing.");

            _context.SubscriptionEvents.Add(evt);
            await _subscriptionRepository.UpdateAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            var updated = await _subscriptionRepository.GetByIdAsync(subscription.Id);
            return Ok(new
            {
                message = "Subscription will continue renewing.",
                subscription = MapToDto(updated!)
            });
        }

        /// <summary>
        /// Cancels the current subscription at period end (local-only flow used by
        /// the web app). The mobile flow (<see cref="Cancel"/> at PUT status) also
        /// cancels the Stripe subscription; this route is kept for existing clients.
        /// </summary>
        [Authorize]
        [HttpPost("current/cancel")]
        public async Task<IActionResult> CancelCurrent()
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

        private static SubscriptionDto MapToDto(LocalSubscription s)
        {
            return new SubscriptionDto(
                s.Id,
                BuildPlanDto(s.Plan),
                s.Status.ToString(),
                s.StartedAt,
                s.CurrentPeriodStart,
                s.CurrentPeriodEnd,
                s.CancelAtPeriodEnd
            );
        }

        // Presentation copy for each plan, served by the API so the mobile app never
        // hardcodes it. Amount/name/limits come from the database.
        private static readonly Dictionary<string, (string Tagline, string Description, string[] Features, bool Popular)> PlanMarketing =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Basic"] = (
                    "CONNECTED LIVING",
                    "Essential basic features for your day-to-day.",
                    new[]
                    {
                        "IoT control from mobile app",
                        "Remote On/Off",
                        "Live device status",
                        "Basic notifications"
                    },
                    false),
                ["Plus"] = (
                    "SMART PROPERTY MANAGEMENT",
                    "Total customization and automated energy efficiency.",
                    new[]
                    {
                        "Scenarios (Night/Savings Mode)",
                        "Detailed usage history",
                        "Personalized smart alerts",
                        "Multi-user configuration",
                        "Optimized experience without limits"
                    },
                    true),
            };

        private static SubscriptionPlanDto BuildPlanDto(SubscriptionPlan p)
        {
            // "Professional" is the legacy name for the Plus tier.
            var key = p.Name.Equals("Professional", StringComparison.OrdinalIgnoreCase) ? "Plus" : p.Name;
            PlanMarketing.TryGetValue(key, out var m);
            return new SubscriptionPlanDto(
                p.Id,
                p.Name,
                p.MonthlyPrice,
                p.MaxPropertiesLimit,
                p.UnlimitedProperties,
                m.Tagline,
                m.Description,
                m.Features,
                m.Popular
            );
        }
    }
}
