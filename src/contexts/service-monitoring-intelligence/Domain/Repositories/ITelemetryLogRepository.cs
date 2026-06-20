using System.Threading.Tasks;
using Nexora.Domain.Entities;

namespace Nexora.Domain.Repositories
{
    public interface ITelemetryLogRepository
    {
        Task AddAsync(TelemetryLog log);
    }
}
