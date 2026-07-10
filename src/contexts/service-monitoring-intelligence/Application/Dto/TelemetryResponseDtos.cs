using System.ComponentModel;

namespace Nexora.Application.Dto
{
    /// <summary>
    /// Response from the telemetry ingestion endpoint.
    /// Contains the valve command that should be forwarded to the physical device.
    /// </summary>
    public record TelemetryCommandResponseDto(
        [property: Description("Valve command to execute on the device: 'NONE', 'CLOSE', or 'OPEN'")] string ValveCommand
    );

    /// <summary>
    /// Most recent telemetry reading for a specific device.
    /// Contains all sensor values from the last transmission.
    /// </summary>
    public record TelemetryLatestDto(
        [property: Description("Device identifier")] string DeviceId,
        [property: Description("Water flow reading in L/min")] double WaterReading,
        [property: Description("Gas concentration reading in ppm")] double GasReading,
        [property: Description("Presence/motion detection reading")] bool PresenceReading,
        [property: Description("Electrical current reading in Amperes")] double ElectricityReading,
        [property: Description("True when voltage is within normal range (220V)")] bool VoltageOk,
        [property: Description("UTC timestamp of the reading")] DateTime Timestamp
    );
}
