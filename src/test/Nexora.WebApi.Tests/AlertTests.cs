using System;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Xunit;

namespace Nexora.WebApi.Tests
{
    public class AlertTests
    {
        [Fact]
        public void CreateAlert_WithValidData_ShouldInitializeCorrectly()
        {
            // Arrange
            var severity = AlertSeverity.Critical;
            var type = "Critical Gas Leak Detected";
            var timestamp = DateTime.UtcNow;
            var deviceId = "ESP32-HW-01";

            // Act
            var alert = new Alert(severity, type, timestamp, deviceId);

            // Assert
            Assert.Equal(severity, alert.Severity);
            Assert.Equal(type, alert.Type);
            Assert.Equal(timestamp, alert.Timestamp);
            Assert.Equal(deviceId, alert.DeviceId);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void CreateAlert_WithInvalidDeviceId_ShouldThrowArgumentException(string invalidDeviceId)
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => new Alert(AlertSeverity.Warning, "Gas Leak", DateTime.UtcNow, invalidDeviceId));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void CreateAlert_WithInvalidType_ShouldThrowArgumentException(string invalidType)
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => new Alert(AlertSeverity.Warning, invalidType, DateTime.UtcNow, "ESP32-HW-01"));
        }
    }
}
