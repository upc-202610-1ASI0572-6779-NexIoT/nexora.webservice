using Nexora.Domain.Entities;
using System.Threading.Tasks;

namespace Nexora.Domain.Repositories
{
    public interface ISubscriptionRepository
    {
        Task<Subscription?> GetByLandlordIdAsync(long landlordId);
        Task<Subscription?> GetByIdAsync(long id);
        Task AddAsync(Subscription subscription);
        Task UpdateAsync(Subscription subscription);
    }
}
