using System;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Xunit;

namespace Nexora.WebApi.Tests
{
    public class DeviceTests
    {
        [Fact]
        public void CreateDevice_WithValidData_ShouldInitializeCorrectly()
        {
            // Arrange
            var id = "ESP32-HW-99";
            var status = ConnectionStatus.Offline;
            var syncTime = DateTime.UtcNow;

            // Act
            var device = new Device(id, status, syncTime);

            // Assert
            Assert.Equal(id, device.Id);
            Assert.Equal(status, device.ConnectionStatus);
            Assert.Equal(syncTime, device.LastSyncAt);
            Assert.Null(device.PropertyId);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void CreateDevice_WithInvalidId_ShouldThrowArgumentException(string invalidId)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => new Device(invalidId, ConnectionStatus.Offline, DateTime.UtcNow));
        }

        [Fact]
        public void AssignToProperty_ShouldUpdatePropertyId()
        {
            // Arrange
            var device = new Device("ESP32-HW-99", ConnectionStatus.Offline, DateTime.UtcNow);
            long expectedPropertyId = 123;

            // Act
            device.AssignToProperty(expectedPropertyId);

            // Assert
            Assert.Equal(expectedPropertyId, device.PropertyId);
        }

        [Fact]
        public void UpdateSync_ShouldUpdateStatusAndSyncTime()
        {
            // Arrange
            var device = new Device("ESP32-HW-99", ConnectionStatus.Offline, DateTime.UtcNow.AddMinutes(-5));
            var newStatus = ConnectionStatus.Online;
            var newSyncTime = DateTime.UtcNow;

            // Act
            device.UpdateSync(newStatus, newSyncTime);

            // Assert
            Assert.Equal(newStatus, device.ConnectionStatus);
            Assert.Equal(newSyncTime, device.LastSyncAt);
        }
    }
}
