using System.Threading.Tasks;
using Nexora.Application.Dto;

namespace Nexora.Application.Services
{
    public interface ITelemetryProcessor
    {
        Task ProcessAsync(TelemetryPayloadDto payload);
    }
}
