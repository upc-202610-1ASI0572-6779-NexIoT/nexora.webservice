using System;

namespace Nexora.Application.Dto
{
    public record TenantDto(
        long Id,
        long? PropertyId,
        long? UserId,
        string FirstName,
        string LastName,
        string Country,
        string City,
        string Address,
        string? PhoneNumber,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    public record CreateTenantDto(
        long PropertyId,
        string FirstName,
        string LastName,
        string Country,
        string City,
        string Address,
        string? PhoneNumber
    );
}
