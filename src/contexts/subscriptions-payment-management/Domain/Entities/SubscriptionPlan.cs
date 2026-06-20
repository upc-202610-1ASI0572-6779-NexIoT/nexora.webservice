using System;

namespace Nexora.Domain.Entities
{
    public class SubscriptionPlan
    {
        public long Id { get; private set; }
        public string Name { get; private set; } = null!;
        public decimal MonthlyPrice { get; private set; }
        public int MaxPropertiesLimit { get; private set; }
        public bool UnlimitedProperties { get; private set; }
        public bool IsActive { get; private set; }

        private SubscriptionPlan() { }

        public SubscriptionPlan(string name, decimal monthlyPrice, int maxPropertiesLimit, bool unlimitedProperties = false)
        {
            Name = name;
            MonthlyPrice = monthlyPrice;
            MaxPropertiesLimit = maxPropertiesLimit;
            UnlimitedProperties = unlimitedProperties;
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void UpdatePrice(decimal monthlyPrice)
        {
            MonthlyPrice = monthlyPrice;
        }
    }
}
