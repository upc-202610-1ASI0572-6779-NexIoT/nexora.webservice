using System.ComponentModel;

namespace Nexora.Application.Dto
{
    /// <summary>
    /// Aggregated consumption summary with chart data and per-property breakdown.
    /// The most complex response in the API: includes linked service flags, consumption metrics,
    /// time-series chart data, and per-property status.
    /// </summary>
    public record ConsumptionSummaryResponseDto(
        [property: Description("True when at least one electricity device is linked")] bool HasElectricityLinked,
        [property: Description("True when at least one gas device is linked")] bool HasGasLinked,
        [property: Description("True when at least one water device is linked")] bool HasWaterLinked,
        [property: Description("Current period consumption metrics")] ConsumptionMetricsDto Consumption,
        [property: Description("Time-series data for chart rendering")] ChartDataDto ChartData,
        [property: Description("Per-property consumption breakdown")] List<PropertyBreakdownDto> PropertyBreakdown
    );

    public record ConsumptionMetricsDto(
        [property: Description("Energy consumption metric")] ConsumptionMetricDto Energy,
        [property: Description("Gas consumption metric")] ConsumptionMetricDto Gas,
        [property: Description("Water consumption metric")] ConsumptionMetricDto Water,
        [property: Description("Projected monthly cost estimate")] ProjectedCostsDto ProjectedCosts
    );

    public record ConsumptionMetricDto(
        [property: Description("Formatted consumption value")] string Value,
        [property: Description("Unit of measurement (e.g., 'kWh', 'm³')")] string Unit,
        [property: Description("Trend vs previous period (e.g., '+12.5%')")] string Trend,
        [property: Description("Trend direction for UI coloring: 'success' for decrease, 'danger' for increase")] string TrendVariant
    );

    public record ProjectedCostsDto(
        [property: Description("Estimated total cost for the period")] string Value,
        [property: Description("Monthly budget limit")] string BudgetLimit,
        [property: Description("Budget utilization percentage (0-100)")] double BudgetPercent
    );

    public record ChartDataDto(
        [property: Description("Month labels for the X axis (e.g., ['JAN', 'FEB', ...])")] List<string> Months,
        [property: Description("Energy consumption per month")] List<double> Energy,
        [property: Description("Gas consumption per month")] List<double> Gas,
        [property: Description("Water consumption per month")] List<double> Water
    );

    public record PropertyBreakdownDto(
        [property: Description("Property identifier")] long Id,
        [property: Description("Property name")] string Name,
        [property: Description("Property location (city, country)")] string Location,
        [property: Description("Energy consumption for the current month")] double Energy,
        [property: Description("Gas consumption for the current month")] double Gas,
        [property: Description("Water consumption for the current month")] double Water,
        [property: Description("Load status: 'optimal', 'monitor', or 'high-load'")] string Status
    );

    /// <summary>
    /// Live consumption data with configurable time range.
    /// Returns time-bucketed averages for each utility type.
    /// </summary>
    public record LiveConsumptionDto(
        [property: Description("Time labels for the X axis")] List<string> Labels,
        [property: Description("Average gas reading per time bucket")] List<double> Gas,
        [property: Description("Average electricity reading per time bucket")] List<double> Electricity,
        [property: Description("Average water reading per time bucket")] List<double> Water
    );
}
