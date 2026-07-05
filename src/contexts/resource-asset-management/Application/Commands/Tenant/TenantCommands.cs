using MediatR;

namespace Nexora.Application.Commands.Tenant
{
    public record CreateTenantCommand(
        long PropertyId,
        string FirstName,
        string LastName,
        string Country,
        string City,
        string Address,
        string? PhoneNumber
    ) : IRequest<long>;

    public record UpdateTenantCommand(
        long TenantId,
        string FirstName,
        string LastName,
        string Country,
        string City,
        string Address,
        string? PhoneNumber
    ) : IRequest<bool>;

    public record DeleteTenantCommand(
        long TenantId
    ) : IRequest<bool>;
}
