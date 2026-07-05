using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public async Task<DateTime?> GetContinuousFlowStartTimeAsync(string deviceId)
        {
            // Find the latest log where water was NOT flowing (<= 0.05)
            var lastZeroFlowLog = await _context.TelemetryLogs
                .Where(t => t.DeviceId == deviceId && t.WaterReading <= 0.05)
                .OrderByDescending(t => t.Timestamp)
                .FirstOrDefaultAsync();

            if (lastZeroFlowLog != null)
            {
                // Find the first log with flow (> 0.05) that came after the last zero-flow log.
                var firstFlowLog = await _context.TelemetryLogs
                    .Where(t => t.DeviceId == deviceId && t.WaterReading > 0.05 && t.Timestamp > lastZeroFlowLog.Timestamp)
                    .OrderBy(t => t.Timestamp)
                    .FirstOrDefaultAsync();
                
                return firstFlowLog?.Timestamp;
            }
            else
            {
                // If there has never been a zero-flow log, the water has been flowing since the first log of the device.
                var oldestLog = await _context.TelemetryLogs
                    .Where(t => t.DeviceId == deviceId && t.WaterReading > 0.05)
                    .OrderBy(t => t.Timestamp)
                    .FirstOrDefaultAsync();
                
                return oldestLog?.Timestamp;
            }
        }
    }
}
