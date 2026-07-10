using System.ComponentModel;

namespace Nexora.Application.Dto
{
    /// <summary>
    /// Full tenant details returned by individual tenant lookup.
    /// Contains personal information, property assignment, and timestamps.
    /// </summary>
    public record TenantDetailResponseDto(
        [property: Description("Unique tenant identifier")] long Id,
        [property: Description("ID of the assigned property")] long? PropertyId,
        [property: Description("ID of the linked user account, or null if not linked")] long? UserId,
        [property: Description("Tenant first name")] string FirstName,
        [property: Description("Tenant last name")] string LastName,
        [property: Description("Country of residence")] string Country,
        [property: Description("City of residence")] string City,
        [property: Description("Full address")] string Address,
        [property: Description("Phone number, if provided")] string? PhoneNumber,
        [property: Description("UTC timestamp when the tenant record was created")] DateTime CreatedAt,
        [property: Description("UTC timestamp when the tenant record was last updated")] DateTime UpdatedAt
    );
}
