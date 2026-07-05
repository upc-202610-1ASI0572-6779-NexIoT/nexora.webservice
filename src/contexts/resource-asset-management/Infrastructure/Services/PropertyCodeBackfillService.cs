using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Services;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Services
{
    public class PropertyCodeBackfillService
    {
        private readonly NexoraDbContext _context;
        private readonly IPropertyCodeGenerator _generator;

        public PropertyCodeBackfillService(NexoraDbContext context, IPropertyCodeGenerator generator)
        {
            _context = context;
            _generator = generator;
        }

        public async Task EnsurePropertyCodesAsync()
        {
            var properties = await _context.Properties
                .Where(p => string.IsNullOrEmpty(p.PropertyCode))
                .ToListAsync();

            if (!properties.Any()) return;

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var prop in properties)
                {
                    var code = await _generator.GenerateAsync(prop.PropertyType);
                    prop.SetPropertyCode(code);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
