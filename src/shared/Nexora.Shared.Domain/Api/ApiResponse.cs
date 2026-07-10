using System.ComponentModel;

namespace Nexora.Shared.Domain.Api
{
    /// <summary>
    /// Standardized error response returned by all API endpoints when a request fails.
    /// The Code field identifies the error category (e.g., "Conflict", "NotFound").
    /// The Message field contains a human-readable description localized via Accept-Language.
    /// </summary>
    public record ErrorResponse(
        [property: Description("Error category identifier (e.g., Conflict, NotFound, Unauthorized)")] string Code,
        [property: Description("Human-readable error description, localized based on Accept-Language header")] string Message
    );

    /// <summary>
    /// Simple success response containing only a confirmation message.
    /// Used for PUT/POST operations that modify state but return no entity.
    /// </summary>
    public record MessageResponse(
        [property: Description("Confirmation or status message, localized based on Accept-Language header")] string Message
    );
}
