using System.Collections.Generic;
using System.Threading.Tasks;
using Nexora.Domain.Entities;

namespace Nexora.Domain.Repositories
{
    public interface ITenantRepository
    {
        Task<Tenant?> GetByIdAsync(long id);
        Task<IEnumerable<Tenant>> GetByPropertyIdAsync(long propertyId);
        Task AddAsync(Tenant tenant);
        Task UpdateAsync(Tenant tenant);
        Task DeleteAsync(Tenant tenant);
    }
}
