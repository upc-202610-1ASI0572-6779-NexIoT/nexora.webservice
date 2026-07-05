using MediatR;
using Nexora.Domain.Enums;

namespace Nexora.Application.Commands.Property
{
    public record CreatePropertyCommand(
        string Name, 
        string? Description,
        PropertyType Type,
        string Country,
        string City,
        string Address,
        bool IsSecurityModeArmed, 
        long UserId
    ) : IRequest<long>;

    public record UpdatePropertyStatusCommand(long PropertyId, PropertyStatus NewStatus) : IRequest<bool>;

    public record UpdatePropertyCommand(
        long PropertyId,
        string Name,
        string? Description,
        PropertyType Type,
        string Country,
        string City,
        string Address,
        PropertyStatus Status,
        bool IsSecurityModeArmed
    ) : IRequest<bool>;
}
