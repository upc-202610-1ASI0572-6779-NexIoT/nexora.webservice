using System;
using System.Collections.Generic;
using System.Linq;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Xunit;

namespace Nexora.WebApi.Tests.Steps
{
    public class AlertManagementSteps
    {
        private List<Alert> _systemAlerts = new();
        private List<Alert> _filteredResults = new();

        [Fact]
        public void Scenario_FilterAlertsBySeverityAndType()
        {
            // Given the system has registered alerts
            GivenTheSystemHasRegisteredAlerts();

            // When the landlord filters alerts by severity "Critical" and type "Gas"
            WhenTheLandlordFiltersAlertsBySeverityAndType(AlertSeverity.Critical, "Gas");

            // Then the result should contain 1 alert
            ThenTheResultShouldContainAlerts(1);

            // And the alert should have type "Critical Gas Leak Detected" and severity "Critical"
            AndTheAlertShouldHaveTypeAndSeverity("Critical Gas Leak Detected", AlertSeverity.Critical);
        }

        private void GivenTheSystemHasRegisteredAlerts()
        {
            _systemAlerts = new List<Alert>
            {
                new Alert(AlertSeverity.Critical, "Critical Gas Leak Detected", DateTime.UtcNow, "ESP32-HW-01"),
                new Alert(AlertSeverity.Warning, "Low Voltage Warning", DateTime.UtcNow, "ESP32-HW-02"),
                new Alert(AlertSeverity.Critical, "Intrusion Alert", DateTime.UtcNow, "ESP32-HW-01")
            };
        }

        private void WhenTheLandlordFiltersAlertsBySeverityAndType(AlertSeverity severity, string typeQuery)
        {
            _filteredResults = _systemAlerts
                .Where(a => a.Severity == severity && a.Type.Contains(typeQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void ThenTheResultShouldContainAlerts(int expectedCount)
        {
            Assert.Equal(expectedCount, _filteredResults.Count);
        }

        private void AndTheAlertShouldHaveTypeAndSeverity(string expectedType, AlertSeverity expectedSeverity)
        {
            var alert = _filteredResults.First();
            Assert.Equal(expectedType, alert.Type);
            Assert.Equal(expectedSeverity, alert.Severity);
        }
    }
}
