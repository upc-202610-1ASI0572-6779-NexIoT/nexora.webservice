namespace Nexora.Application.Dto
{
    public record UpdateProfileDto(
        string FirstName,
        string LastName,
        string Country,
        string City,
        string Address,
        string? PhoneNumber
    );
}