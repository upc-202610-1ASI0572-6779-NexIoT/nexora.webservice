using System;
using Nexora.Domain.Enums;

namespace Nexora.WebApi.DTOs
{
    public record LandlordDto(long Id, long UserId, string FirstName, string LastName, string? PhoneNumber);

    public record PropertyDto(
        long Id,
        string PropertyCode,
        string Name,
        string? Description,
        PropertyType PropertyType,
        string Country,
        string City,
        string Address,
        PropertyStatus Status,
        bool IsSecurityModeArmed,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        LandlordDto Landlord
    );
}
