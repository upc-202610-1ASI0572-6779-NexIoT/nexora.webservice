using Nexora.Domain.Entities;
using Nexora.Domain.Services;

namespace Nexora.Application.Services
{
    public class SubscriptionPolicy : ISubscriptionPolicy
    {
        public bool CanCreateProperty(Subscription subscription, int currentPropertyCount)
        {
            if (subscription.Plan.UnlimitedProperties)
                return true;

            return currentPropertyCount < subscription.Plan.MaxPropertiesLimit;
        }

        public bool CanAddDevice(Subscription subscription, int currentDeviceCount)
        {
            return true;
        }
    }
}
