namespace Nexora.Application.Dto
{
    public record ProfileDto(
        string Email,
        string FirstName,
        string LastName,
        bool IsActive,
        string Country,
        string City,
        string Address,
        string? PhoneNumber
    );
}