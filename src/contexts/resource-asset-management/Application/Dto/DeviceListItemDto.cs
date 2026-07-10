using System.ComponentModel;

namespace Nexora.Application.Dto
{
    /// <summary>
    /// Compact device representation returned by the device list endpoint.
    /// Contains identification, connection status, latest telemetry reading, and valve state.
    /// </summary>
    public record DeviceListItemDto(
        [property: Description("Unique device identifier (e.g., 'water_001')")] string Id,
        [property: Description("Human-readable device name")] string Name,
        [property: Description("MAC address of the device, if registered")] string? MacAddress,
        [property: Description("Signal strength indicator")] int? Rssi,
        [property: Description("Current firmware version installed on the device")] string? FirmwareVersion,
        [property: Description("True when the firmware is not the latest stable version")] bool IsFirmwareOutdated,
        [property: Description("Connection status: 'Online' or 'Offline'")] string ConnectionStatus,
        [property: Description("UTC timestamp of the last sync with the device")] DateTime? LastSyncAt,
        [property: Description("ID of the assigned property, or null if unassigned")] long? PropertyId,
        [property: Description("Name of the assigned property, or 'Unassigned'")] string PropertyName,
        [property: Description("Latest telemetry reading with unit (e.g., '3.2 A', '220 V (Normal)')")] string LatestReading,
        [property: Description("Current valve state: 'OPEN' or 'CLOSED'")] string ValveState
    );

    /// <summary>
    /// Key performance indicators for the device fleet under the current user's properties.
    /// All values are returned as strings for display purposes.
    /// </summary>
    public record DeviceKpiDto(
        [property: Description("Percentage of online devices (e.g., '85%')")] string OperationalStatus,
        [property: Description("Average messages per second in the last 60 seconds")] string GatewayLoad,
        [property: Description("Total number of active alerts across all devices")] string ActiveAlerts,
        [property: Description("Number of devices running outdated firmware")] string FirmwareDrift
    );

    /// <summary>
    /// Device entity returned after registration or update.
    /// Exposes only client-relevant fields, hiding internal domain state.
    /// </summary>
    public record DeviceResponseDto(
        [property: Description("Unique device identifier")] string Id,
        [property: Description("Human-readable device name")] string? Name,
        [property: Description("MAC address")] string? MacAddress,
        [property: Description("Connection status: 'Online' or 'Offline'")] string ConnectionStatus,
        [property: Description("Firmware version")] string? FirmwareVersion,
        [property: Description("Assigned property ID, or null")] long? PropertyId,
        [property: Description("UTC timestamp of last sync")] DateTime? LastSyncAt
    );
}
