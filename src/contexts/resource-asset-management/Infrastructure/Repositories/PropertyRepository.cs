using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Repositories
{
    public class PropertyRepository : IPropertyRepository
    {
        private readonly NexoraDbContext _context;

        public PropertyRepository(NexoraDbContext context)
        {
            _context = context;
        }

        public async Task<Property?> GetByIdAsync(long id)
        {
            return await _context.Set<Property>()
                .Include(p => p.Landlord)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Property?> GetByDeviceIdAsync(string deviceId)
        {
            var device = await _context.Devices
                .Include(d => d.Property)
                .ThenInclude(p => p!.Landlord)
                .FirstOrDefaultAsync(d => d.Id == deviceId);

            return device?.Property;
        }

        public async Task AddAsync(Property property)
        {
            await _context.Set<Property>().AddAsync(property);
        }
    }
}
