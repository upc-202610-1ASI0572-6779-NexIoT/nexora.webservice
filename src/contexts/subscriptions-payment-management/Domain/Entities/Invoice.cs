using System;
using System.Collections.Generic;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Entities
{
    public class Invoice
    {
        public long Id { get; private set; }

        public long SubscriptionId { get; private set; }
        public Subscription Subscription { get; private set; } = null!;

        public decimal Amount { get; private set; }

        public InvoiceStatus Status { get; private set; }

        public DateTime DueDate { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public ICollection<Payment> Payments { get; private set; } = new List<Payment>();

        private Invoice() { }

        public Invoice(long subscriptionId, decimal amount, DateTime dueDate)
        {
            SubscriptionId = subscriptionId;
            Amount = amount;
            Status = InvoiceStatus.Pending;
            DueDate = dueDate;
            CreatedAt = DateTime.UtcNow;
        }

        public void MarkAsPaid()
        {
            Status = InvoiceStatus.Paid;
        }

        public void MarkAsOverdue()
        {
            if (Status == InvoiceStatus.Pending)
            {
                Status = InvoiceStatus.Overdue;
            }
        }

        public void Cancel()
        {
            Status = InvoiceStatus.Cancelled;
        }
    }
}
