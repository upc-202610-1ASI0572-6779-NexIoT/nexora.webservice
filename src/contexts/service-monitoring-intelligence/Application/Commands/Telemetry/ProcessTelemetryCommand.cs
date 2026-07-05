using MediatR;
using Nexora.Application.Dto;

namespace Nexora.Application.Commands.Telemetry
{
    public record ProcessTelemetryCommand(TelemetryPayloadDto Payload) : IRequest;
}
