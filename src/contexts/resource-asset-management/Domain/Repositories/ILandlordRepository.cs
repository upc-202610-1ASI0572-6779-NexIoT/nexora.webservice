using Nexora.Domain.Entities;
using System.Threading.Tasks;

namespace Nexora.Domain.Repositories
{
    public interface ILandlordRepository
    {
        Task<Landlord?> GetByUserIdAsync(long userId);
        Task<Landlord?> GetByIdAsync(long id);
        Task AddAsync(Landlord landlord);
        Task UpdateAsync(Landlord landlord);
    }
}
