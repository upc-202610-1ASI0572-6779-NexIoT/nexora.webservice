using System.ComponentModel;

namespace Nexora.Application.Dto
{
    /// <summary>
    /// Standardized response for profile retrieval and update operations.
    /// When no profile is found, Profile will be null and Message will contain the reason.
    /// </summary>
    public record ProfileResponseDto(
        [property: Description("The user profile data, or null if no profile exists")] ProfileDto? Profile,
        [property: Description("The user type: 'Landlord' or 'Tenant'")] string? Type,
        [property: Description("Optional status message, present when no profile was found")] string? Message
    );
}
