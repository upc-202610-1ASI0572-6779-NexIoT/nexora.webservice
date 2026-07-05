using System;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Entities
{
    public class Device
    {
        public string Id { get; private set; }
        public ConnectionStatus ConnectionStatus { get; private set; }
        public DateTime LastSyncAt { get; private set; }
        public long? PropertyId { get; private set; }
        public Property? Property { get; private set; }

        public void AssignToProperty(long propertyId)
        {
            PropertyId = propertyId;
        }

        #pragma warning disable CS8618
        private Device() { }
        #pragma warning restore CS8618

        public Device(string id, ConnectionStatus connectionStatus, DateTime lastSyncAt)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Device ID cannot be empty or null.", nameof(id));

            Id = id;
            ConnectionStatus = connectionStatus;
            LastSyncAt = lastSyncAt;
        }

        public void UpdateSync(ConnectionStatus status, DateTime syncTime)
        {
            ConnectionStatus = status;
            LastSyncAt = syncTime;
        }
    }
}
