Feature: Alert Query and Filtering
  As a Landlord
  I want to filter the security and telemetry alerts of my properties
  So that I can quickly identify critical risks

  Scenario: Filter alerts by severity and type
    Given the system has registered alerts:
      | DeviceId    | Severity | Type                      |
      | ESP32-HW-01 | Critical | Critical Gas Leak Detected|
      | ESP32-HW-02 | Warning  | Low Voltage Warning       |
      | ESP32-HW-01 | Critical | Intrusion Alert           |
    When the landlord filters alerts by severity "Critical" and type "Gas"
    Then the result should contain 1 alert
    And the alert should have type "Critical Gas Leak Detected" and severity "Critical"
