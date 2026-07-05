using System;
using System.ComponentModel.DataAnnotations;

namespace Nexora.Application.Dto
{
    public class TelemetryPayloadDto
    {
        [Required(ErrorMessage = "DeviceId is required.")]
        public string DeviceId { get; set; } = null!;

        [Required(ErrorMessage = "Timestamp is required.")]
        public long Timestamp { get; set; } // Unix epoch

        [Required(ErrorMessage = "Sensors data block is required.")]
        public SensorDataDto Sensors { get; set; } = null!;
    }

    public class SensorDataDto
    {
        [Range(0, double.MaxValue, ErrorMessage = "Water flow (Lpm) must be a positive value.")]
        public double WaterLpm { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Gas level (Ppm) must be a positive value.")]
        public double GasPpm { get; set; }

        [Range(0, 1, ErrorMessage = "Presence must be 0 or 1.")]
        public int Presence { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Electricity (Kwh) must be a positive value.")]
        public double ElectricityKwh { get; set; }

        public bool VoltageOk { get; set; }
    }
}
