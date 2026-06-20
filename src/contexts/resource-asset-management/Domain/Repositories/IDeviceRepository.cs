using System.Threading.Tasks;
using Nexora.Domain.Entities;

namespace Nexora.Domain.Repositories
{
    public interface IDeviceRepository
    {
        Task<Device?> GetByIdAsync(string id);
        Task AddAsync(Device device);
        Task UpdateAsync(Device device);
    }
}
