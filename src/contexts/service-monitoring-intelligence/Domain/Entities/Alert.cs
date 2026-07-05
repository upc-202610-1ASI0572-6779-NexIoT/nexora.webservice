using System;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Entities
{
    public class Alert
    {
        public long Id { get; private set; }
        public AlertSeverity Severity { get; private set; }
        public DateTime Timestamp { get; private set; }
        public string DeviceId { get; private set; }
        public Device Device { get; private set; } = null!;
        public string Type { get; private set; } = null!;

        #pragma warning disable CS8618
        private Alert() { }
        #pragma warning restore CS8618

        public Alert(AlertSeverity severity, string type, DateTime timestamp, string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("DeviceId cannot be empty.", nameof(deviceId));
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Type cannot be empty.", nameof(type));

            Severity = severity;
            Type = type;
            Timestamp = timestamp;
            DeviceId = deviceId;
        }
    }
}
