using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Domain.Resources;
using Swashbuckle.AspNetCore.Annotations;
using Stripe;

using StripeInvoice = Stripe.Invoice;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
        [Route("api/v1/billing-events")]
        [SwaggerTag("Billing Events")]
    public class WebhooksController : ControllerBase
    {
        private readonly NexoraDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly string _webhookSecret;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public WebhooksController(
            NexoraDbContext context,
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IStringLocalizer<SharedMessages> localizer)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _webhookSecret = configuration["Stripe:WebhookSecret"] ?? "whsec_test";
            _localizer = localizer;
        }

        /// <summary>
        /// Handles Stripe webhook events for invoice payment success.
        /// This endpoint is called by Stripe, not by the mobile/web client.
        /// </summary>
        [HttpPost]
        [SwaggerOperation(Summary = "Handle Stripe webhook", Description = "Receives and processes Stripe webhook events. Called by Stripe, not clients.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _webhookSecret,
                    throwOnApiVersionMismatch: false
                );

                if (stripeEvent.Type == EventTypes.InvoicePaymentSucceeded)
                {
                    var stripeInvoice = stripeEvent.Data.Object as StripeInvoice;
                    var stripeSubscriptionId = stripeInvoice?.Parent?.SubscriptionDetails?.SubscriptionId;

                    if (stripeInvoice != null && !string.IsNullOrEmpty(stripeSubscriptionId))
                    {
                        var subscription = await _context.Subscriptions
                            .Include(s => s.Invoices)
                            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);

                        if (subscription != null)
                        {
                            var invoice = subscription.Invoices
                                .OrderByDescending(i => i.CreatedAt)
                                .FirstOrDefault();

                            if (invoice != null && invoice.Status == InvoiceStatus.Pending)
                            {
                                invoice.MarkAsPaid();

                                var transactionId = stripeInvoice.Id ?? "stripe_tx";
                                var payment = new Payment(invoice.Id, invoice.Amount, "stripe", transactionId);
                                payment.Succeed();

                                _context.Payments.Add(payment);

                                var landlordId = subscription.LandlordId;
                                var card = await _context.SavedCards.FirstOrDefaultAsync(c => c.LandlordId == landlordId);
                                if (card == null)
                                {
                                    string brand = "Visa";
                                    string lastFour = "4242";
                                    string expiryMonth = "12";
                                    string expiryYear = "29";
                                    string holderName = "Cardholder User";

                                    string chargeId = null;
                                    if (stripeInvoice.RawJObject != null)
                                    {
                                        chargeId = stripeInvoice.RawJObject["charge"]?.ToString();
                                    }
                                    if (string.IsNullOrEmpty(chargeId) && stripeInvoice.Payments?.Data != null && stripeInvoice.Payments.Data.Any())
                                    {
                                        chargeId = stripeInvoice.Payments.Data.First().Payment?.ChargeId;
                                    }

                                    if (!string.IsNullOrEmpty(chargeId))
                                    {
                                        try
                                        {
                                            var chargeService = new ChargeService();
                                            var charge = await chargeService.GetAsync(chargeId);

                                            if (charge?.PaymentMethodDetails?.Card != null)
                                            {
                                                brand = charge.PaymentMethodDetails.Card.Brand;
                                                lastFour = charge.PaymentMethodDetails.Card.Last4;
                                                expiryMonth = charge.PaymentMethodDetails.Card.ExpMonth.ToString().PadLeft(2, '0');
                                                var expYearStr = charge.PaymentMethodDetails.Card.ExpYear.ToString();
                                                expiryYear = expYearStr.Length > 2 ? expYearStr.Substring(expYearStr.Length - 2) : expYearStr;
                                                holderName = charge.BillingDetails?.Name ?? "Cardholder User";
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            // Fallback to defaults
                                        }
                                    }

                                    card = new SavedCard(
                                        landlordId,
                                        brand,
                                        $"************{lastFour}",
                                        expiryMonth,
                                        expiryYear,
                                        holderName,
                                        "***"
                                    );
                                    _context.SavedCards.Add(card);
                                }

                                var evt = new SubscriptionEvent(
                                    subscription.Id,
                                    "Payment Received",
                                    $"Payment received successfully via webhook. Invoice: {invoice.Id}"
                                );
                                _context.SubscriptionEvents.Add(evt);

                                await _unitOfWork.SaveChangesAsync();
                            }
                        }
                    }
                }

                return Ok();
            }
            catch (StripeException ex)
            {
                return BadRequest(new ErrorResponse("BadRequest", string.Format(_localizer["Stripe_Error"], ex.Message)));
            }
            catch (Exception ex)
            {
                return BadRequest(new ErrorResponse("BadRequest", _localizer["Internal_ServerError"]));
            }
        }
    }
}
