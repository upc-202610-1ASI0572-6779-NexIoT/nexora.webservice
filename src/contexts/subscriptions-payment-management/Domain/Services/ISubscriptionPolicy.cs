using Nexora.Domain.Entities;

namespace Nexora.Domain.Services
{
    public interface ISubscriptionPolicy
    {
        bool CanCreateProperty(Subscription subscription, int currentPropertyCount);
        bool CanAddDevice(Subscription subscription, int currentDeviceCount);
    }
}
