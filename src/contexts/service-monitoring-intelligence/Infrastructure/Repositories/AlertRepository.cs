using System.Threading.Tasks;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Repositories
{
    public class AlertRepository : IAlertRepository
    {
        private readonly NexoraDbContext _context;

        public AlertRepository(NexoraDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Alert alert)
        {
            await _context.Alerts.AddAsync(alert);
        }
    }
}
