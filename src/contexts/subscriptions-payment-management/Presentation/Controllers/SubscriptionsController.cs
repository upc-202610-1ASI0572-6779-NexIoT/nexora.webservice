using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Nexora.Application.Dto;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Domain.Resources;
using Stripe;
using Stripe.Checkout;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

using StripeInvoice = Stripe.Invoice;
using StripeSubscription = Stripe.Subscription;
using LocalInvoice = Nexora.Domain.Entities.Invoice;
using LocalSubscription = Nexora.Domain.Entities.Subscription;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [SwaggerTag("Subscriptions & Billing")]
    public class SubscriptionsController : ControllerBase
    {
        private static readonly List<string> AllowedPaymentMethodTypes = new() { "card" };

        private readonly NexoraDbContext _context;
        private readonly ILandlordRepository _landlordRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _config;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public SubscriptionsController(
            NexoraDbContext context,
            ILandlordRepository landlordRepository,
            ISubscriptionRepository subscriptionRepository,
            IUnitOfWork unitOfWork,
            IConfiguration config,
            IStringLocalizer<SharedMessages> localizer)
        {
            _context = context;
            _landlordRepository = landlordRepository;
            _subscriptionRepository = subscriptionRepository;
            _unitOfWork = unitOfWork;
            _config = config;
            _localizer = localizer;
        }

        #region Subscription Plans & Configuration

        /// <summary>
        /// Returns available subscription plans, optionally filtered by target user type.
        /// </summary>
        [HttpGet("api/v1/subscription-plans")]
        [SwaggerOperation(Summary = "Get available subscription plans", Description = "Retrieves all active subscription plans, optionally filtered by target user type (e.g., landlord or tenant).")]
        [ProducesResponseType(typeof(List<SubscriptionPlanDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPlans([FromQuery] string? target = null)
        {
            var query = _context.SubscriptionPlans.Where(p => p.IsActive);

            if (!string.IsNullOrEmpty(target))
            {
                query = query.Where(p => p.TargetUser.ToLower() == target.ToLower());
            }

            var plans = await query
                .OrderBy(p => p.MonthlyPrice)
                .ToListAsync();

            var dtos = plans.Select(BuildPlanDto).ToList();
            return Ok(dtos);
        }

        /// <summary>
        /// Returns the Stripe publishable key needed to initialise the flutter_stripe SDK.
        /// </summary>
        [HttpGet("api/v1/payment-configuration")]
        [SwaggerOperation(Summary = "Get payment configuration", Description = "Returns the Stripe publishable key needed to initialise the flutter_stripe SDK on the client side.")]
        [ProducesResponseType(typeof(StripeConfigDto), StatusCodes.Status200OK)]
        public IActionResult GetConfiguration()
        {
            var key = _config["Stripe:PublishableKey"] ?? string.Empty;
            return Ok(new StripeConfigDto(key));
        }

        #endregion

        #region Subscriptions

        /// <summary>
        /// Returns all subscriptions for the authenticated user.
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/subscriptions")]
        [SwaggerOperation(Summary = "List subscriptions", Description = "Returns all subscriptions for the authenticated landlord.")]
        [ProducesResponseType(typeof(List<SubscriptionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var subscriptions = await _context.Subscriptions
                .Include(s => s.Plan)
                .Where(s => s.LandlordId == landlord.Id)
                .OrderByDescending(s => s.StartedAt)
                .ToListAsync();

            var dtos = subscriptions.Select(MapToDto).ToList();
            return Ok(dtos);
        }

        /// <summary>
        /// Activates a new subscription for the authenticated landlord.
        /// </summary>
        [Authorize]
        [HttpPost("api/v1/subscriptions")]
        [SwaggerOperation(Summary = "Activate a subscription", Description = "Activates a new subscription for the authenticated landlord. Supports local-dev mock flow and live Stripe flow.")]
        [ProducesResponseType(typeof(ActivateSubscriptionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Activate([FromBody] ActivateSubscriptionRequest request)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var landlord = await _landlordRepository.GetByUserIdAsync(userId.Value);
            if (landlord == null)
                return BadRequest(new ErrorResponse("BadRequest", _localizer["Profile_LandlordNotFound"]));

            var plan = await _context.SubscriptionPlans.FindAsync(request.SubscriptionPlanId);
            if (plan == null)
                return BadRequest(new ErrorResponse("BadRequest", _localizer["Subscription_PlanNotFound"]));

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
                            return BadRequest(new ErrorResponse("BadRequest", _localizer["Subscription_AlreadySubscribed"]));
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

                localInvoice.MarkAsPaid();
                _context.Invoices.Add(localInvoice);
                await _unitOfWork.SaveChangesAsync();

                var localPayment = new Payment(localInvoice.Id, localInvoice.Amount, "local", "local_tx_" + Guid.NewGuid());
                localPayment.Succeed();
                _context.Payments.Add(localPayment);

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
                    if (existing.SubscriptionPlanId == plan.Id)
                    {
                        return BadRequest(new ErrorResponse("BadRequest", _localizer["Subscription_AlreadySubscribed"]));
                    }

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

                    existing.ChangePlan(plan.Id, DateTime.UtcNow.AddMonths(1));
                    await _subscriptionRepository.UpdateAsync(existing);

                    var changePlanEvt = new SubscriptionEvent(
                        existing.Id,
                        "Plan Changed",
                        $"Upgraded/Downgraded plan to: {plan.Name}. Price: ${plan.MonthlyPrice}/mo."
                    );
                    _context.SubscriptionEvents.Add(changePlanEvt);

                    await _unitOfWork.SaveChangesAsync();

                    var prorationClientSecret = await GetClientSecretIfPaymentRequiredAsync(stripeSub.LatestInvoice);

                    return Ok(new
                    {
                        Subscription = MapToDto(existing),
                        ClientSecret = prorationClientSecret
                    });
                }
            }

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

                    var customerSvc = new CustomerService();
                    await customerSvc.UpdateAsync(landlord.StripeCustomerId, new CustomerUpdateOptions
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
            else if (!string.IsNullOrEmpty(request.PaymentMethodId))
            {
                pmId = request.PaymentMethodId;
                try
                {
                    var paymentMethodService = new PaymentMethodService();
                    await paymentMethodService.AttachAsync(pmId, new PaymentMethodAttachOptions
                    {
                        Customer = landlord.StripeCustomerId
                    });

                    var customerSvc = new CustomerService();
                    await customerSvc.UpdateAsync(landlord.StripeCustomerId, new CustomerUpdateOptions
                    {
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = pmId
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Stripe PaymentMethod attach failed: {ex.Message}");
                }
            }

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
                    SaveDefaultPaymentMethod = "on_subscription",
                    PaymentMethodTypes = AllowedPaymentMethodTypes
                },
                Expand = new List<string> { "latest_invoice.confirmation_secret", "latest_invoice.payments.data.payment" }
            };
            if (!string.IsNullOrEmpty(pmId))
            {
                subOptions.DefaultPaymentMethod = pmId;
            }
            StripeSubscription stripeSubscription = await stripeSubscriptionService.CreateAsync(subOptions);

            StripeInvoice stripeInvoice = stripeSubscription.LatestInvoice;
            var clientSecret = await GetClientSecretIfPaymentRequiredAsync(stripeInvoice);

            var now = DateTime.UtcNow;
            var periodEnd = now.AddMonths(1);

            var subscription = new LocalSubscription(landlord.Id, plan.Id, now, periodEnd);

            await _subscriptionRepository.AddAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            var subFromDb = await _subscriptionRepository.GetByIdAsync(subscription.Id);

            var dueDate = now.AddDays(7);
            var invoice = new LocalInvoice(subscription.Id, plan.MonthlyPrice, dueDate);

            invoice.MarkAsPaid();
            _context.Invoices.Add(invoice);
            await _unitOfWork.SaveChangesAsync();

            var transactionId = stripeInvoice?.Id ?? "stripe_tx";
            var payment = new Payment(invoice.Id, invoice.Amount, "stripe", transactionId);
            payment.Succeed();
            _context.Payments.Add(payment);

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
                $"Plan {plan.Name} activated. ${plan.MonthlyPrice}/mo.");

            _context.SubscriptionEvents.Add(evt);

            await _unitOfWork.SaveChangesAsync();

            var subDto = MapToDto(subFromDb!);

            return Ok(new ActivateSubscriptionResponse(subDto, plan.MonthlyPrice, dueDate, invoice.Id, clientSecret));
        }

        /// <summary>
        /// Returns the authenticated landlord's current active subscription.
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/subscriptions/current")]
        [SwaggerOperation(Summary = "Get current subscription", Description = "Returns the authenticated landlord's current active subscription.")]
        [ProducesResponseType(typeof(CurrentSubscriptionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCurrent()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var subscription = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
            if (subscription == null)
                return Ok(new CurrentSubscriptionResponseDto(null, _localizer["Subscription_NotFound"]));

            var dto = MapToDto(subscription);
            return Ok(new CurrentSubscriptionResponseDto(dto, null));
        }

        /// <summary>
        /// Returns a specific subscription by ID.
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/subscriptions/{subscriptionId:long}")]
        [SwaggerOperation(Summary = "Get subscription by ID", Description = "Returns details for a specific subscription.")]
        [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long subscriptionId)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null || subscription.LandlordId != landlord.Id)
                return NotFound(new ErrorResponse("NotFound", _localizer["Subscription_NotFound"]));

            return Ok(MapToDto(subscription));
        }

        /// <summary>
        /// Updates a subscription's state (cancel or resume). 
        /// Send { "status": "cancelled" } to cancel or { "status": "active" } to resume.
        /// </summary>
        [Authorize]
        [HttpPatch("api/v1/subscriptions/{subscriptionId:long}")]
        [SwaggerOperation(Summary = "Update subscription state", Description = "Transitions subscription state: cancel or resume. Body: { \"status\": \"cancelled\" | \"active\" }.")]
        [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> UpdateSubscription(long subscriptionId, [FromBody] UpdateSubscriptionStateRequest request)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null || subscription.LandlordId != landlord.Id)
                return NotFound(new ErrorResponse("NotFound", _localizer["Subscription_NotFound"]));

            if (string.IsNullOrEmpty(request.Status))
                return BadRequest(new ErrorResponse("BadRequest", _localizer["Subscription_InvalidStatus"]));

            if (request.Status.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
            {
                if (subscription.Status != SubscriptionStatus.Active && subscription.Status != SubscriptionStatus.PastDue)
                    return BadRequest(new ErrorResponse("BadRequest", string.Format(_localizer["Subscription_CannotCancel"], subscription.Status)));

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
                        return StatusCode(502, new ErrorResponse("BadGateway", string.Format(_localizer["Stripe_Error"], ex.Message)));
                    }
                }

                subscription.Cancel();

                var evt = new SubscriptionEvent(subscription.Id, "Subscription Cancelled",
                    $"Cancelled at period end: {subscription.CurrentPeriodEnd:yyyy-MM-dd}.");
                _context.SubscriptionEvents.Add(evt);

                await _subscriptionRepository.UpdateAsync(subscription);
                await _unitOfWork.SaveChangesAsync();

                var updated = await _subscriptionRepository.GetByIdAsync(subscription.Id);
                return Ok(MapToDto(updated!));
            }
            else if (request.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                if (!subscription.CancelAtPeriodEnd)
                    return BadRequest(new ErrorResponse("BadRequest", _localizer["Subscription_NotScheduledForCancellation"]));

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
                        return StatusCode(502, new ErrorResponse("BadGateway", string.Format(_localizer["Stripe_Error"], ex.Message)));
                    }
                }

                subscription.UndoCancel();

                var evt = new SubscriptionEvent(subscription.Id, "Subscription Resumed",
                    "Cancellation undone; subscription will continue renewing.");
                _context.SubscriptionEvents.Add(evt);

                await _subscriptionRepository.UpdateAsync(subscription);
                await _unitOfWork.SaveChangesAsync();

                var updated = await _subscriptionRepository.GetByIdAsync(subscription.Id);
                return Ok(MapToDto(updated!));
            }

            return BadRequest(new ErrorResponse("BadRequest", _localizer["Subscription_InvalidStatus"]));
        }

        #endregion

        #region Checkout Sessions & Syncs

        /// <summary>
        /// Creates a Stripe Checkout Session for the selected subscription plan.
        /// </summary>
        [Authorize]
        [HttpPost("api/v1/checkout-sessions")]
        [SwaggerOperation(Summary = "Create Stripe Checkout session", Description = "Creates a Stripe Checkout Session for the selected subscription plan.")]
        [ProducesResponseType(typeof(CheckoutSessionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] ActivateSubscriptionRequest request)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var plan = await _context.SubscriptionPlans.FindAsync(request.SubscriptionPlanId);
            if (plan == null) return BadRequest(new ErrorResponse("BadRequest", _localizer["Subscription_PlanNotFound"]));

            try
            {
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
                return StatusCode(502, new ErrorResponse("BadGateway", string.Format(_localizer["Stripe_Error"], ex.Message)));
            }
        }

        /// <summary>
        /// Reconciles the local subscription with the landlord's active Stripe subscription.
        /// </summary>
        [Authorize]
        [HttpPost("api/v1/subscription-syncs")]
        [SwaggerOperation(Summary = "Sync subscription from Stripe", Description = "Reconciles the local subscription with the landlord's active Stripe subscription.")]
        [ProducesResponseType(typeof(SyncSubscriptionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> Sync()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));
            if (string.IsNullOrEmpty(landlord.StripeCustomerId))
                return Ok(new SyncSubscriptionResponseDto(null));

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
                    return Ok(new SyncSubscriptionResponseDto(currentLocal == null ? null : MapToDto(currentLocal)));
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
                return Ok(new SyncSubscriptionResponseDto(full == null ? null : MapToDto(full)));
            }
            catch (StripeException ex)
            {
                return StatusCode(502, new ErrorResponse("BadGateway", string.Format(_localizer["Stripe_Error"], ex.Message)));
            }
        }

        #endregion

        #region Payment Methods

        /// <summary>
        /// Returns all saved payment methods for the authenticated landlord.
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/payment-methods")]
        [SwaggerOperation(Summary = "Get all saved payment methods", Description = "Returns all saved payment methods (cards) for the authenticated landlord.")]
        [ProducesResponseType(typeof(PaymentMethodsResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPaymentMethods()
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

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

            return Ok(new PaymentMethodsResponseDto(savedCards));
        }

        /// <summary>
        /// Creates a new saved payment method.
        /// </summary>
        [Authorize]
        [HttpPost("api/v1/payment-methods")]
        [SwaggerOperation(Summary = "Create payment method", Description = "Creates a new saved payment method (card) for the authenticated landlord.")]
        [ProducesResponseType(typeof(PaymentMethodDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreatePaymentMethod([FromBody] UpdatePaymentMethodRequest request)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            if (!string.IsNullOrEmpty(request.PaymentMethodId))
            {
                try
                {
                    var paymentMethodService = new PaymentMethodService();
                    var pm = await paymentMethodService.GetAsync(request.PaymentMethodId);

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

                    var brand = pm.Card?.Brand ?? "Visa";
                    var lastFour = pm.Card?.Last4 ?? "4242";
                    var expMonth = pm.Card?.ExpMonth.ToString().PadLeft(2, '0') ?? "12";
                    var expYearStr = pm.Card?.ExpYear.ToString() ?? "29";
                    var expYear = expYearStr.Length > 2 ? expYearStr.Substring(expYearStr.Length - 2) : expYearStr;
                    var holderName = request.HolderName ?? pm.BillingDetails?.Name ?? "Cardholder User";

                    var existingCard = await _context.SavedCards
                        .FirstOrDefaultAsync(c => c.LandlordId == landlord.Id);

                    if (existingCard != null)
                    {
                        existingCard.Update(brand, $"************{lastFour}", expMonth, expYear, holderName, "***");
                    }
                    else
                    {
                        existingCard = new SavedCard(landlord.Id, brand, $"************{lastFour}", expMonth, expYear, holderName, "***");
                        _context.SavedCards.Add(existingCard);
                    }

                    await _context.SaveChangesAsync();

                    return Ok(new PaymentMethodDetailDto(
                        existingCard.Id, existingCard.Brand, existingCard.LastFour, existingCard.FullNumber,
                        existingCard.ExpiryMonth, existingCard.ExpiryYear, existingCard.HolderName, existingCard.Cvv
                    ));
                }
                catch (Exception ex)
                {
                    return BadRequest(new ErrorResponse("BadRequest", string.Format(_localizer["Stripe_Error"], ex.Message)));
                }
            }

            var card = await _context.SavedCards.FirstOrDefaultAsync(c => c.LandlordId == landlord.Id);
            if (card != null)
            {
                card.Update(request.Brand, request.FullNumber, request.ExpiryMonth, request.ExpiryYear, request.HolderName, request.Cvv);
                await _context.SaveChangesAsync();
                return Ok(new PaymentMethodDetailDto(card.Id, card.Brand, card.LastFour, card.FullNumber, card.ExpiryMonth, card.ExpiryYear, card.HolderName, card.Cvv));
            }

            card = new SavedCard(landlord.Id, request.Brand, request.FullNumber, request.ExpiryMonth, request.ExpiryYear, request.HolderName, request.Cvv);
            _context.SavedCards.Add(card);
            await _context.SaveChangesAsync();

            return Ok(new PaymentMethodDetailDto(card.Id, card.Brand, card.LastFour, card.FullNumber, card.ExpiryMonth, card.ExpiryYear, card.HolderName, card.Cvv));
        }

        /// <summary>
        /// Returns a specific payment method by ID.
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/payment-methods/{paymentMethodId:long}")]
        [SwaggerOperation(Summary = "Get payment method by ID", Description = "Returns a specific saved payment method by its ID.")]
        [ProducesResponseType(typeof(PaymentMethodDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPaymentMethodById(long paymentMethodId)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var card = await _context.SavedCards
                .FirstOrDefaultAsync(c => c.Id == paymentMethodId && c.LandlordId == landlord.Id);

            if (card == null) return NotFound(new ErrorResponse("NotFound", _localizer["PaymentMethod_NotFound"]));

            return Ok(new PaymentMethodDetailDto(
                card.Id, card.Brand, card.LastFour, card.FullNumber,
                card.ExpiryMonth, card.ExpiryYear, card.HolderName, card.Cvv
            ));
        }

        /// <summary>
        /// Updates a specific payment method by ID.
        /// </summary>
        [Authorize]
        [HttpPatch("api/v1/payment-methods/{paymentMethodId:long}")]
        [SwaggerOperation(Summary = "Update payment method", Description = "Partially updates a specific saved payment method by its ID.")]
        [ProducesResponseType(typeof(PaymentMethodDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePaymentMethod(long paymentMethodId, [FromBody] UpdatePaymentMethodRequest request)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var card = await _context.SavedCards
                .FirstOrDefaultAsync(c => c.Id == paymentMethodId && c.LandlordId == landlord.Id);

            if (card == null) return NotFound(new ErrorResponse("NotFound", _localizer["PaymentMethod_NotFound"]));

            card.Update(request.Brand, request.FullNumber, request.ExpiryMonth, request.ExpiryYear, request.HolderName, request.Cvv);

            await _context.SaveChangesAsync();

            return Ok(new PaymentMethodDetailDto(
                card.Id, card.Brand, card.LastFour, card.FullNumber,
                card.ExpiryMonth, card.ExpiryYear, card.HolderName, card.Cvv
            ));
        }

        /// <summary>
        /// Deletes a specific payment method by ID.
        /// </summary>
        [Authorize]
        [HttpDelete("api/v1/payment-methods/{paymentMethodId:long}")]
        [SwaggerOperation(Summary = "Delete payment method", Description = "Removes a specific saved payment method by its ID.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePaymentMethod(long paymentMethodId)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var card = await _context.SavedCards
                .FirstOrDefaultAsync(c => c.Id == paymentMethodId && c.LandlordId == landlord.Id);

            if (card == null) return NotFound(new ErrorResponse("NotFound", _localizer["PaymentMethod_NotFound"]));

            _context.SavedCards.Remove(card);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        #endregion

        #region Invoices

        /// <summary>
        /// Returns all invoices for the authenticated landlord's subscriptions.
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/invoices")]
        [SwaggerOperation(Summary = "Get invoices", Description = "Returns all invoices for the authenticated landlord's subscriptions.")]
        [ProducesResponseType(typeof(InvoicesResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetInvoices([FromQuery] long? subscriptionId = null)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            IQueryable<LocalInvoice> query = _context.Invoices;

            if (subscriptionId.HasValue)
            {
                query = query.Where(i => i.SubscriptionId == subscriptionId.Value);
            }
            else
            {
                var subscription = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
                if (subscription == null)
                    return Ok(new InvoicesResponseDto(new List<InvoiceDto>()));
                query = query.Where(i => i.SubscriptionId == subscription.Id);
            }

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new InvoiceDto(
                    i.Id,
                    i.Amount,
                    i.Status.ToString(),
                    i.DueDate,
                    i.CreatedAt
                ))
                .ToListAsync();

            return Ok(new InvoicesResponseDto(invoices));
        }

        /// <summary>
        /// Returns a specific invoice by ID.
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/invoices/{invoiceId:long}")]
        [SwaggerOperation(Summary = "Get invoice by ID", Description = "Returns details for a specific invoice. Use ?format=pdf to download.")]
        [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetInvoiceById(long invoiceId, [FromQuery] string? format = null)
        {
            var landlord = await GetLandlordAsync();
            if (landlord == null) return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Invoice_NotFound"]));

            if (!string.IsNullOrEmpty(format) && format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ErrorResponse("BadRequest", _localizer["Report_UnsupportedFormat"]));
            }

            return Ok(new InvoiceDto(
                invoice.Id,
                invoice.Amount,
                invoice.Status.ToString(),
                invoice.DueDate,
                invoice.CreatedAt
            ));
        }

        #endregion

        #region Private Helpers

        private async Task<Landlord?> GetLandlordAsync()
        {
            var userId = User.GetUserId();
            if (userId == null) return null;

            var landlord = await _landlordRepository.GetByUserIdAsync(userId.Value);
            if (landlord == null)
            {
                var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.UserId == userId.Value);
                if (tenant != null)
                {
                    landlord = new Landlord(
                        userId.Value,
                        tenant.FirstName,
                        tenant.LastName,
                        tenant.Country,
                        tenant.City,
                        tenant.Address,
                        tenant.PhoneNumber
                    );
                    _context.Landlords.Add(landlord);
                    await _context.SaveChangesAsync();
                }
            }
            return landlord;
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

        private static async Task<string?> GetClientSecretIfPaymentRequiredAsync(StripeInvoice? invoice)
        {
            var clientSecret = invoice?.ConfirmationSecret?.ClientSecret;
            if (string.IsNullOrEmpty(clientSecret)) return null;

            var paymentIntentId = invoice?.Payments?.Data?.FirstOrDefault()?.Payment?.PaymentIntentId;
            if (string.IsNullOrEmpty(paymentIntentId))
            {
                return clientSecret;
            }

            var paymentIntent = await new PaymentIntentService().GetAsync(paymentIntentId);
            var needsConfirmation = paymentIntent.Status is "requires_payment_method"
                or "requires_confirmation"
                or "requires_action";
            return needsConfirmation ? clientSecret : null;
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

        private static readonly Dictionary<string, (string Tagline, string Description, string[] Features, bool Popular)> PlanMarketing =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["landlord_Basic"] = (
                    "PROPERTY ESSENTIALS",
                    "Essential features for managing your properties.",
                    new[]
                    {
                        "Hasta 3 propiedades gestionadas",
                        "Historial de consumo de 3 meses",
                        "Análisis mensual de consumo",
                        "Alertas preventivas por web y correo",
                        "Modo seguridad manual para propiedades vacías",
                        "Exportación de reportes en PDF"
                    },
                    false),
                ["landlord_Professional"] = (
                    "SMART PROPERTY MANAGEMENT",
                    "Total control, detailed analytics and priority VIP support.",
                    new[]
                    {
                        "Propiedades gestionadas ilimitadas",
                        "Historial ilimitado e interactivo",
                        "Análisis por hora, día, semana y mes",
                        "Alertas push, correo y panel web",
                        "Panel flotante de emergencia",
                        "Exportación PDF y Excel",
                        "Roles, permisos y soporte VIP"
                    },
                    true),
                ["tenant_Basic"] = (
                    "CONNECTED LIVING",
                    "Essential basic features for your day-to-day IoT control.",
                    new[]
                    {
                        "Control IoT desde la app móvil",
                        "Encendido y apagado remoto",
                        "Estado de dispositivos en vivo",
                        "Notificaciones básicas"
                    },
                    false),
                ["tenant_Plus"] = (
                    "ADVANCED COMFORT",
                    "Automated routines, alerts and predictives expenses.",
                    new[]
                    {
                        "Escenas y rutinas inteligentes",
                        "Historial de uso detallado",
                        "Alertas inteligentes personalizadas",
                        "Estimación predictiva de gastos",
                        "Configuración multiusuario",
                        "Alertas de emergencia"
                    },
                    true),
            };

        private static SubscriptionPlanDto BuildPlanDto(SubscriptionPlan p)
        {
            var key = $"{p.TargetUser}_{p.Name}";
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
                m.Popular,
                p.TargetUser
            );
        }

        #endregion
    }

    public record UpdateSubscriptionStateRequest(string? Status);
}
