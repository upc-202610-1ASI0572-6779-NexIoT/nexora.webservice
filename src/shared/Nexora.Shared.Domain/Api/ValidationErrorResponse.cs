using System.ComponentModel;

namespace Nexora.Shared.Domain.Api
{
    /// <summary>
    /// Structured validation error response preserving per-field error details.
    /// Returned when ASP.NET model binding detects one or more validation errors.
    /// </summary>
    public record ValidationErrorResponse(
        [property: Description("Validation error category identifier")] string Code,
        [property: Description("Human-readable summary of the validation failure")] string Message,
        [property: Description("Dictionary of field names to their specific error messages")] IDictionary<string, string[]> Errors
    );
}
