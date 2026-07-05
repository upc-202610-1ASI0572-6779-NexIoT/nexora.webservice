using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Repositories
{
    public class DeviceRepository : IDeviceRepository
    {
        private readonly NexoraDbContext _context;

        public DeviceRepository(NexoraDbContext context)
        {
            _context = context;
        }

        public async Task<Device?> GetByIdAsync(string id)
        {
            return await _context.Devices.FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task AddAsync(Device device)
        {
            await _context.Devices.AddAsync(device);
        }

        public Task UpdateAsync(Device device)
        {
            _context.Devices.Update(device);
            return Task.CompletedTask;
        }
    }
}
