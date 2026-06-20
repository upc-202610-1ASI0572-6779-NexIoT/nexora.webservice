using System;
using System.Threading.Tasks;
using Nexora.Domain.Entities;

namespace Nexora.Domain.Repositories
{
    public interface IPropertyRepository
    {
        Task<Property?> GetByIdAsync(long id);
        Task<Property?> GetByDeviceIdAsync(string deviceId);
        Task AddAsync(Property property);
    }
}
