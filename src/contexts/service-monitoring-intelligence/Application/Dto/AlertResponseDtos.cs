using System.ComponentModel;

namespace Nexora.Application.Dto
{
    /// <summary>
    /// Compact alert representation for list views.
    /// Includes severity, associated device, latest reading, and resolution status.
    /// </summary>
    public record AlertListItemDto(
        [property: Description("Unique alert identifier")] long Id,
        [property: Description("Alert severity: 'Low', 'Medium', 'High', or 'Critical'")] string Severity,
        [property: Description("UTC timestamp when the alert was triggered")] DateTime Timestamp,
        [property: Description("ID of the device that triggered the alert")] string DeviceId,
        [property: Description("Alert type description (e.g., 'GasLeak', 'Overcurrent')")] string Type,
        [property: Description("Name of the property where the device is located")] string PropertyName,
        [property: Description("Telemetry reading value at the time of the alert")] double Reading,
        [property: Description("Resolution status: 'active', 'pending', or 'resolved'")] string Status
    );

    /// <summary>
    /// Detailed alert information including the associated device, property, ticket, and recent telemetry history.
    /// </summary>
    public record AlertDetailDto(
        [property: Description("Unique alert identifier")] long Id,
        [property: Description("Alert severity level")] string Severity,
        [property: Description("UTC timestamp when the alert was triggered")] DateTime Timestamp,
        [property: Description("Device identifier that triggered the alert")] string DeviceId,
        [property: Description("Alert type (e.g., 'GasLeak', 'WaterLeak')")] string Type,
        [property: Description("Associated device details, or null if device was removed")] AlertDeviceDto? Device,
        [property: Description("Associated maintenance ticket, or null if no ticket was created")] AlertTicketDto? Ticket,
        [property: Description("Last 10 telemetry readings before the alert timestamp")] List<AlertTelemetryEntryDto> RecentTelemetry
    );

    public record AlertDeviceDto(
        [property: Description("Device identifier")] string Id,
        [property: Description("Device connection status")] string ConnectionStatus,
        [property: Description("UTC timestamp of last sync")] DateTime? LastSyncAt,
        [property: Description("Property where the device is installed")] AlertPropertyDto? Property
    );

    public record AlertPropertyDto(
        [property: Description("Property identifier")] long Id,
        [property: Description("Property name")] string Name,
        [property: Description("Property address")] string Address,
        [property: Description("Property city")] string City,
        [property: Description("Property country")] string Country,
        [property: Description("Whether security mode is currently armed")] bool IsSecurityModeArmed
    );

    public record AlertTicketDto(
        [property: Description("Ticket identifier")] long Id,
        [property: Description("Ticket status: 'Open', 'InProgress', 'Resolved'")] string Status,
        [property: Description("Name of the person assigned to resolve the ticket")] string? AssignedTo,
        [property: Description("UTC timestamp when the ticket was created")] DateTime CreatedAt,
        [property: Description("UTC timestamp when the ticket was resolved, or null")] DateTime? ResolvedAt
    );

    public record AlertTelemetryEntryDto(
        [property: Description("Water flow reading in L/min")] double WaterReading,
        [property: Description("Gas concentration in ppm")] double GasReading,
        [property: Description("Presence/motion sensor reading")] bool PresenceReading,
        [property: Description("Electrical current in Amperes")] double ElectricityReading,
        [property: Description("True when voltage is within normal range")] bool VoltageOk,
        [property: Description("UTC timestamp of the reading")] DateTime Timestamp
    );

    /// <summary>
    /// Maintenance ticket created for an alert.
    /// </summary>
    public record MaintenanceTicketDto(
        [property: Description("Ticket identifier")] long Id,
        [property: Description("ID of the associated alert")] long AlertId,
        [property: Description("Current status: 'Open', 'InProgress', or 'Resolved'")] string Status,
        [property: Description("Name of the assigned technician, if any")] string? AssignedTo,
        [property: Description("UTC timestamp when the ticket was created")] DateTime CreatedAt,
        [property: Description("UTC timestamp when the ticket was resolved, or null if still open")] DateTime? ResolvedAt
    );
}
