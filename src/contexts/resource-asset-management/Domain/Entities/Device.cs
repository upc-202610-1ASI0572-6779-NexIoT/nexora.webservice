using System;
using Nexora.Domain.Enums;

namespace Nexora.Domain.Entities
{
    public class Device
    {
        public string Id { get; private set; }
        public string? Name { get; private set; }
        public string? MacAddress { get; private set; }
        public int? Rssi { get; private set; }
        public string? FirmwareVersion { get; private set; }
        public ConnectionStatus ConnectionStatus { get; private set; }
        public DateTime LastSyncAt { get; private set; }
        public long? PropertyId { get; private set; }
        public Property? Property { get; private set; }

        public void AssignToProperty(long? propertyId)
        {
            PropertyId = propertyId;
        }

        public void UpdateMacAddress(string? macAddress)
        {
            MacAddress = macAddress;
        }

        public void UpdateName(string? name)
        {
            Name = name;
        }

        public void UpdateRssi(int? rssi)
        {
            Rssi = rssi;
        }

        public void UpdateFirmwareVersion(string? version)
        {
            FirmwareVersion = version;
        }

        #pragma warning disable CS8618
        private Device() { }
        #pragma warning restore CS8618

        public Device(string id, ConnectionStatus connectionStatus, DateTime lastSyncAt, string? macAddress = null, string? name = null, int? rssi = null, string? firmwareVersion = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Device ID cannot be empty or null.", nameof(id));

            Id = id;
            ConnectionStatus = connectionStatus;
            LastSyncAt = lastSyncAt;
            MacAddress = macAddress;
            Name = name ?? id;
            Rssi = rssi;
            FirmwareVersion = firmwareVersion;
        }

        public void UpdateSync(ConnectionStatus status, DateTime syncTime)
        {
            ConnectionStatus = status;
            LastSyncAt = syncTime;
        }
    }
}
