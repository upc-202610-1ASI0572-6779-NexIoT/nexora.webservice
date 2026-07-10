using System;

namespace Nexora.Domain.Entities
{
    public class DeviceSystemLog
    {
        public long Id { get; private set; }
        public string DeviceId { get; private set; }
        public Device Device { get; private set; } = null!;
        public string Type { get; private set; } = null!; // "success", "warning", "info", "danger", etc.
        public string Title { get; private set; } = null!;
        public string Message { get; private set; } = null!;
        public DateTime Timestamp { get; private set; }

        #pragma warning disable CS8618
        private DeviceSystemLog() { }
        #pragma warning restore CS8618

        public DeviceSystemLog(string deviceId, string type, string title, string message, DateTime timestamp)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("DeviceId cannot be empty.", nameof(deviceId));
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Type cannot be empty.", nameof(type));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be empty.", nameof(title));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be empty.", nameof(message));

            DeviceId = deviceId;
            Type = type;
            Title = title;
            Message = message;
            Timestamp = timestamp;
        }
    }
}
