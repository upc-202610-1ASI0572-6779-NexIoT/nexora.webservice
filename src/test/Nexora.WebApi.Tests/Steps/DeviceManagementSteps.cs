using System;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Xunit;

namespace Nexora.WebApi.Tests.Steps
{
    public class DeviceManagementSteps
    {
        private long _propertyId;
        private Device _device;

        [Fact]
        public void Scenario_SuccessfullyRegisterANewDeviceToAProperty()
        {
            // Given a property with ID 5 exists
            GivenAPropertyWithIDExists(5);

            // And a device with ID "ESP32-HW-01" is not associated with any property
            AndADeviceWithIDIsNotAssociatedWithAnyProperty("ESP32-HW-01");

            // When the landlord associates the device "ESP32-HW-01" with property ID 5
            WhenTheLandlordAssociatesTheDeviceWithPropertyID("ESP32-HW-01", 5);

            // Then the device should have property ID 5 assigned
            ThenTheDeviceShouldHavePropertyIDAssigned(5);
        }

        private void GivenAPropertyWithIDExists(long propertyId)
        {
            _propertyId = propertyId;
        }

        private void AndADeviceWithIDIsNotAssociatedWithAnyProperty(string deviceId)
        {
            _device = new Device(deviceId, ConnectionStatus.Offline, DateTime.UtcNow);
            Assert.Null(_device.PropertyId);
        }

        private void WhenTheLandlordAssociatesTheDeviceWithPropertyID(string deviceId, long propertyId)
        {
            Assert.Equal(deviceId, _device.Id);
            _device.AssignToProperty(propertyId);
        }

        private void ThenTheDeviceShouldHavePropertyIDAssigned(long propertyId)
        {
            Assert.Equal(propertyId, _device.PropertyId);
        }
    }
}
