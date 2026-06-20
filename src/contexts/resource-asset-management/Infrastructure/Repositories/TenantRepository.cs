using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Repositories
{
    public class TenantRepository : ITenantRepository
    {
        private readonly NexoraDbContext _context;

        public TenantRepository(NexoraDbContext context)
        {
            _context = context;
        }

        public async Task<Tenant?> GetByIdAsync(long id)
        {
            return await _context.Tenants
                .Include(t => t.Property)
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<IEnumerable<Tenant>> GetByPropertyIdAsync(long propertyId)
        {
            return await _context.Tenants
                .Where(t => t.PropertyId == propertyId)
                .ToListAsync();
        }

        public async Task AddAsync(Tenant tenant)
        {
            await _context.Tenants.AddAsync(tenant);
        }

        public async Task UpdateAsync(Tenant tenant)
        {
            _context.Tenants.Update(tenant);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(Tenant tenant)
        {
            _context.Tenants.Remove(tenant);
            await Task.CompletedTask;
        }
    }
}
