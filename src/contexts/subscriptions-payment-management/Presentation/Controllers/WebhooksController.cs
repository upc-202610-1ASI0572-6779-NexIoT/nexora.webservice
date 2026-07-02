using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

using StripeInvoice = Stripe.Invoice;
using LocalInvoice = Nexora.Domain.Entities.Invoice;
using LocalSubscription = Nexora.Domain.Entities.Subscription;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/payments/webhook")]
    public class WebhooksController : ControllerBase
    {
        private readonly NexoraDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly string _webhookSecret;

        public WebhooksController(
            NexoraDbContext context,
            IUnitOfWork unitOfWork,
            IConfiguration configuration)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _webhookSecret = configuration["Stripe:WebhookSecret"] ?? "whsec_test";
        }

        [HttpPost]
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

                if (stripeEvent.Type == Stripe.EventTypes.InvoicePaymentSucceeded)
                {
                    var stripeInvoice = stripeEvent.Data.Object as StripeInvoice;
                    var stripeSubscriptionId = stripeInvoice?.Parent?.SubscriptionDetails?.SubscriptionId;

                    if (stripeInvoice != null && !string.IsNullOrEmpty(stripeSubscriptionId))
                    {
                        // Find local subscription
                        var subscription = await _context.Subscriptions
                            .Include(s => s.Invoices)
                            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);

                        if (subscription != null)
                        {
                            // Find the latest pending invoice
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
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
