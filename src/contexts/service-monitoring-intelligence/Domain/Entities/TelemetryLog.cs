using System;

namespace Nexora.Domain.Entities
{
    public class TelemetryLog
    {
        public long Id { get; private set; }
        public string DeviceId { get; private set; }
        public Device Device { get; private set; } = null!;
        public double WaterReading { get; private set; }
        public double GasReading { get; private set; }
        public bool PresenceReading { get; private set; }
        public double ElectricityReading { get; private set; }
        public bool VoltageOk { get; private set; }
        public DateTime Timestamp { get; private set; }

        #pragma warning disable CS8618
        private TelemetryLog() { }
        #pragma warning restore CS8618

        public TelemetryLog(string deviceId, double waterReading, double gasReading, bool presenceReading, double electricityReading, bool voltageOk, DateTime timestamp)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("DeviceId cannot be empty.", nameof(deviceId));

            DeviceId = deviceId;
            WaterReading = waterReading;
            GasReading = gasReading;
            PresenceReading = presenceReading;
            ElectricityReading = electricityReading;
            VoltageOk = voltageOk;
            Timestamp = timestamp;
        }
    }
}
