using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Repositories
{
    public class LandlordRepository : ILandlordRepository
    {
        private readonly NexoraDbContext _context;

        public LandlordRepository(NexoraDbContext context)
        {
            _context = context;
        }

        public async Task<Landlord?> GetByUserIdAsync(long userId)
        {
            return await _context.Landlords
                .Include(l => l.Properties)
                .FirstOrDefaultAsync(l => l.UserId == userId);
        }

        public async Task<Landlord?> GetByIdAsync(long id)
        {
            return await _context.Landlords
                .Include(l => l.Properties)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task AddAsync(Landlord landlord)
        {
            await _context.Landlords.AddAsync(landlord);
        }

        public async Task UpdateAsync(Landlord landlord)
        {
            _context.Landlords.Update(landlord);
            await Task.CompletedTask;
        }

    }
}
