namespace Nexora.Application.Dto
{
    public record LoginDto(string Email, string Password);
    
    public record RegisterDto(
        string Email, 
        string Password, 
        string FirstName, 
        string LastName, 
        string Country, 
        string City, 
        string Address, 
        string? PhoneNumber
    );

    public record AuthResponseDto(string Email, string Token, long UserId, SubscriptionDto? Subscription = null);
}
