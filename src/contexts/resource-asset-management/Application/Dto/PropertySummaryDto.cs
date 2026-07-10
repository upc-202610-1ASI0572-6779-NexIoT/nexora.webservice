using System.ComponentModel;

namespace Nexora.Application.Dto
{
    /// <summary>
    /// Summary of the user's properties: total count and number with security mode armed.
    /// </summary>
    public record PropertySummaryDto(
        [property: Description("Total number of properties accessible to the user")] int Total,
        [property: Description("Number of properties with ACTIVE status and security mode armed")] int ProtectedCount
    );

    /// <summary>
    /// Total property count for the current user.
    /// </summary>
    public record PropertyStatsDto(
        [property: Description("Total number of properties accessible to the user")] int Total
    );

    /// <summary>
    /// Dashboard metric: count of properties with security mode armed.
    /// </summary>
    public record PropertyDashboardDto(
        [property: Description("Number of properties with ACTIVE status and security mode armed")] int Count
    );
}
