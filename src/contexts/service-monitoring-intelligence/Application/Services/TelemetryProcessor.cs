using System.Threading.Tasks;
using MediatR;
using Nexora.Application.Dto;
using Nexora.Application.Commands.Telemetry;

namespace Nexora.Application.Services
{
    public class TelemetryProcessor : ITelemetryProcessor
    {
        private readonly IMediator _mediator;

        public TelemetryProcessor(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task ProcessAsync(TelemetryPayloadDto payload)
        {
            await _mediator.Send(new ProcessTelemetryCommand(payload));
        }
    }
}
