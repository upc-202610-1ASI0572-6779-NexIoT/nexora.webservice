namespace Nexora.Application.Dto
{
    public record LoginDto(string Email, string Password, string Platform = "web");

    public record RegisterLandlordDto(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string Country,
        string City,
        string Address,
        string? PhoneNumber
    );

    public record RegisterTenantDto(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string Country,
        string City,
        string Address,
        string? PhoneNumber,
        long? PropertyId = null,
        long? ExistingTenantId = null
    );

    public record AuthResponseDto(
        string Email,
        string Token,
        long UserId,
        string UserableType,
        long UserableId
    );

    public record ErrorResponseDto(
        string Error,
        string Message
    );
}