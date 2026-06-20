using System;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Entities
{
    public class Payment
    {
        public long Id { get; private set; }

        public long InvoiceId { get; private set; }
        public Invoice Invoice { get; private set; } = null!;

        public decimal Amount { get; private set; }

        public PaymentStatus Status { get; private set; }

        public string Provider { get; private set; } = null!;

        public string ProviderTransactionId { get; private set; } = null!;

        public DateTime PaidAt { get; private set; }

        private Payment() { }

        public Payment(long invoiceId, decimal amount, string provider, string providerTransactionId)
        {
            InvoiceId = invoiceId;
            Amount = amount;
            Status = PaymentStatus.Pending;
            Provider = provider;
            ProviderTransactionId = providerTransactionId;
            PaidAt = DateTime.UtcNow;
        }

        public void Succeed()
        {
            Status = PaymentStatus.Succeeded;
        }

        public void Fail()
        {
            Status = PaymentStatus.Failed;
        }

        public void Refund()
        {
            if (Status == PaymentStatus.Succeeded)
            {
                Status = PaymentStatus.Refunded;
            }
        }
    }
}
