Feature: Device Management
  As a Landlord
  I want to register and manage my IoT devices
  So that I can monitor the resource usage of my properties

  Scenario: Successfully register a new device to a property
    Given a property with ID 5 exists
    And a device with ID "ESP32-HW-01" is not associated with any property
    When the landlord associates the device "ESP32-HW-01" with property ID 5
    Then the device should have property ID 5 assigned
