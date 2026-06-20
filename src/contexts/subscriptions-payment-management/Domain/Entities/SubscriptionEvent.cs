using System;

namespace Nexora.Domain.Entities
{
    public class SubscriptionEvent
    {
        public long Id { get; private set; }

        public long SubscriptionId { get; private set; }
        public Subscription Subscription { get; private set; } = null!;

        public string EventType { get; private set; } = null!;

        public string Description { get; private set; } = null!;

        public DateTime CreatedAt { get; private set; }

        private SubscriptionEvent() { }

        public SubscriptionEvent(long subscriptionId, string eventType, string description)
        {
            SubscriptionId = subscriptionId;
            EventType = eventType;
            Description = description;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
