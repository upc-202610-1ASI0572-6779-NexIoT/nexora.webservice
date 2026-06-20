namespace Nexora.Interface.DTOs
{
    public record TenantDetailDto(
        long Id,
        long PropertyId,
        long? UserId,
        string FirstName,
        string LastName,
        string Country,
        string City,
        string Address,
        string? PhoneNumber,
        string? Email,
        bool? IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}
