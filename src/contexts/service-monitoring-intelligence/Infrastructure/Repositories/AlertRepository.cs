using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public async Task<bool> HasActiveAlertAsync(string deviceId, string type)
        {
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            return await _context.Alerts
                .AnyAsync(a => a.DeviceId == deviceId && a.Type == type && a.Timestamp >= oneMinuteAgo
                    && !_context.MaintenanceTickets.Any(t => t.AlertId == a.Id && t.Status == Nexora.Domain.Enums.TicketStatus.Resolved));
        }
    }
}
