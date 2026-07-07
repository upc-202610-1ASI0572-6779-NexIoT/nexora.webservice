using System;
using System.Threading.Tasks;
using Nexora.Domain.Entities;

namespace Nexora.Domain.Repositories
{
    public interface ITelemetryLogRepository
    {
        Task AddAsync(TelemetryLog log);
        Task<DateTime?> GetContinuousFlowStartTimeAsync(string deviceId);
        Task<TelemetryLog?> GetLatestTelemetryLogAsync(string deviceId);
    }
}
