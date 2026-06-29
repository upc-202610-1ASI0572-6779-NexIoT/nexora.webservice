using System;
using System.Collections.Generic;

namespace Nexora.Application.Dto
{
    /// <summary>
    /// Aggregated, chart-ready consumption report for a single metric (water or
    /// electricity) over a selected time range. Built from raw <c>TelemetryLog</c>
    /// readings so the mobile Reports module can show real, up-to-date figures.
    /// </summary>
    public class ConsumptionReportDto
    {
        public string Metric { get; set; } = null!;   // "water" | "electricity"
        public string Range { get; set; } = null!;     // "day" | "week" | "month" | "year"

        public string Unit { get; set; } = null!;      // consumption unit: "L" | "kWh"
        public string RateUnit { get; set; } = null!;  // instantaneous unit: "L/min" | "kW"

        /// <summary>Estimated consumption over the selected period (in <see cref="Unit"/>).</summary>
        public double Total { get; set; }

        /// <summary>Estimated consumption over the immediately preceding period of equal length.</summary>
        public double PreviousTotal { get; set; }

        /// <summary>Percentage change vs the previous period (positive = more usage).</summary>
        public double DeltaPercent { get; set; }

        /// <summary>True when the current period consumed more than the previous one.</summary>
        public bool Increase { get; set; }

        /// <summary>
        /// False when there isn't enough history to compare against the previous
        /// period (e.g. a fresh deployment). The client should then hide the delta
        /// instead of showing a misleading percentage.
        /// </summary>
        public bool Comparable { get; set; }

        /// <summary>Average consumption per chart bucket (e.g. per day / per month).</summary>
        public double Average { get; set; }

        /// <summary>Human label describing the average granularity, e.g. "per day".</summary>
        public string AverageLabel { get; set; } = null!;

        /// <summary>Highest instantaneous reading observed in the period (in <see cref="RateUnit"/>).</summary>
        public double Peak { get; set; }

        /// <summary>Timestamp of the peak reading (UTC), if any.</summary>
        public DateTime? PeakAt { get; set; }

        /// <summary>True when the peak crossed the safe operating threshold.</summary>
        public bool HighUsage { get; set; }

        /// <summary>Safe operating threshold for the metric (in <see cref="RateUnit"/>).</summary>
        public double SafeThreshold { get; set; }

        /// <summary>Per-bucket consumption values used to render the trend line.</summary>
        public List<double> Series { get; set; } = new();

        /// <summary>A small set of evenly spaced labels for the chart X axis.</summary>
        public List<string> AxisLabels { get; set; } = new();

        /// <summary>Breakdown of consumption by contributing device/source.</summary>
        public List<ConsumptionSourceDto> Sources { get; set; } = new();

        public int SampleCount { get; set; }
        public DateTime? LastReadingAt { get; set; }

        /// <summary>False when there is no telemetry to report for the selection.</summary>
        public bool HasData { get; set; }
    }

    public class ConsumptionSourceDto
    {
        public string DeviceId { get; set; } = null!;
        public string Label { get; set; } = null!;
        public double Value { get; set; }        // consumption in the report Unit
        public double SharePercent { get; set; } // share of the total (0-100)
    }
}
