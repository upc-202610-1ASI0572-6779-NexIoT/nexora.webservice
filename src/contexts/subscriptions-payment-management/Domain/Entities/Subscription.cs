using System;
using System.Collections.Generic;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Entities
{
    public class Subscription
    {
        public long Id { get; private set; }

        public long LandlordId { get; private set; }
        public Landlord Landlord { get; private set; } = null!;

        public long SubscriptionPlanId { get; private set; }
        public SubscriptionPlan Plan { get; private set; } = null!;

        public SubscriptionStatus Status { get; private set; }

        public DateTime StartedAt { get; private set; }

        public DateTime CurrentPeriodStart { get; private set; }
        public DateTime CurrentPeriodEnd { get; private set; }

        public bool CancelAtPeriodEnd { get; private set; }

        public DateTime? CancelledAt { get; private set; }
        public string? StripeSubscriptionId { get; private set; }

        public ICollection<Invoice> Invoices { get; private set; } = new List<Invoice>();
        public ICollection<SubscriptionEvent> Events { get; private set; } = new List<SubscriptionEvent>();

        private Subscription() { }

        public Subscription(long landlordId, long subscriptionPlanId, DateTime currentPeriodStart, DateTime currentPeriodEnd)
        {
            LandlordId = landlordId;
            SubscriptionPlanId = subscriptionPlanId;
            Status = SubscriptionStatus.Active;
            StartedAt = DateTime.UtcNow;
            CurrentPeriodStart = currentPeriodStart;
            CurrentPeriodEnd = currentPeriodEnd;
            CancelAtPeriodEnd = false;
        }

        public void MarkAsPastDue()
        {
            if (Status == SubscriptionStatus.Active || Status == SubscriptionStatus.Trialing)
            {
                Status = SubscriptionStatus.PastDue;
            }
        }

        public void Suspend()
        {
            if (Status == SubscriptionStatus.PastDue)
            {
                Status = SubscriptionStatus.Suspended;
            }
        }

        public void Reactivate()
        {
            if (Status == SubscriptionStatus.Suspended || Status == SubscriptionStatus.PastDue)
            {
                Status = SubscriptionStatus.Active;
            }
        }

        public void Cancel()
        {
            CancelAtPeriodEnd = true;
            CancelledAt = DateTime.UtcNow;
        }

        /// <summary>Undoes a pending cancellation, keeping the subscription renewing.</summary>
        public void UndoCancel()
        {
            CancelAtPeriodEnd = false;
            CancelledAt = null;
        }

        public void Expire()
        {
            Status = SubscriptionStatus.Expired;
            CancelAtPeriodEnd = false;
        }

        public void ChangePlan(long newPlanId, DateTime periodEnd)
        {
            SubscriptionPlanId = newPlanId;
            CurrentPeriodEnd = periodEnd;
        }

        public void RenewPeriod(DateTime periodStart, DateTime periodEnd)
        {
            CurrentPeriodStart = periodStart;
            CurrentPeriodEnd = periodEnd;
            Status = SubscriptionStatus.Active;
            CancelAtPeriodEnd = false;
        }

        public void SetStripeSubscriptionId(string stripeSubscriptionId)
        {
            StripeSubscriptionId = stripeSubscriptionId;
        }
    }
}
