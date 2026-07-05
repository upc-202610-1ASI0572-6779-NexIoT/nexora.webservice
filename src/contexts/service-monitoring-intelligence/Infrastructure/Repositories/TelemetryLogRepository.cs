using System.Threading.Tasks;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Repositories
{
    public class TelemetryLogRepository : ITelemetryLogRepository
    {
        private readonly NexoraDbContext _context;

        public TelemetryLogRepository(NexoraDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(TelemetryLog log)
        {
            await _context.TelemetryLogs.AddAsync(log);
        }
    }
}
