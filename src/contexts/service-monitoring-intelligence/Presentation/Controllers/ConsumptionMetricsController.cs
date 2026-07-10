using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nexora.Application.Dto;
using Nexora.Application.Services;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Infrastructure.Persistence;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Domain.Resources;
using Swashbuckle.AspNetCore.Annotations;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/consumption-metrics")]
    [Authorize]
    [SwaggerTag("Consumption Metrics")]
    public class ConsumptionMetricsController : ControllerBase
    {
        private readonly NexoraDbContext _context;
        private readonly IReportService _reportService;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public ConsumptionMetricsController(NexoraDbContext context, IReportService reportService, IStringLocalizer<SharedMessages> localizer)
        {
            _context = context;
            _reportService = reportService;
            _localizer = localizer;
        }

        /// <summary>
        /// Returns consumption metrics. Use ?range=24h for live data, or omit for historical aggregation.
        /// Basic plan: max 3 months history. Supports ?format=pdf for export.
        /// </summary>
        [HttpGet]
        [SwaggerOperation(Summary = "Get consumption metrics", Description = "Returns consumption data. Filter by deviceId, metric, date range. Supports format=pdf for export.")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetConsumptionMetrics(
            [FromQuery] string? deviceId = null,
            [FromQuery] string? metric = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? format = null)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var landlord = await _context.Landlords.FirstOrDefaultAsync(l => l.UserId == userId.Value);
            if (landlord == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Profile_LandlordNotFound"]));

            if (!string.IsNullOrEmpty(format) && format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                return await HandleExportAsync(landlord, userId.Value, 6, format);
            }

            return await GetConsumptionSummaryAsync(landlord, deviceId, metric, startDate, endDate);
        }

        private async Task<IActionResult> GetConsumptionSummaryAsync(Landlord landlord, string? deviceId, string? metric, DateTime? startDate, DateTime? endDate)
        {
            var subscription = await _context.Subscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.LandlordId == landlord.Id);

            var properties = await _context.Properties
                .Where(p => p.LandlordId == landlord.Id)
                .ToListAsync();

            var propertyIds = properties.Select(p => p.Id).ToList();
            var devices = await _context.Devices
                .Where(d => d.PropertyId != null && propertyIds.Contains(d.PropertyId.Value))
                .ToListAsync();

            if (!string.IsNullOrEmpty(deviceId))
                devices = devices.Where(d => d.Id == deviceId).ToList();

            var deviceIds = devices.Select(d => d.Id).ToList();

            var hasElectricityLinked = devices.Any(d => d.Id.ToLower().Contains("voltage") || d.Id.ToLower().Contains("electricity"));
            var hasGasLinked = devices.Any(d => d.Id.ToLower().Contains("gas"));
            var hasWaterLinked = devices.Any(d => d.Id.ToLower().Contains("water") || d.Id.ToLower().Contains("agua"));

            var now = DateTime.UtcNow;
            var defaultMonths = 6;
            if (subscription != null && subscription.Plan.Name.Equals("Basic", StringComparison.OrdinalIgnoreCase))
                defaultMonths = 3;

            var startPeriod = startDate?.ToUniversalTime()
                ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(defaultMonths - 1));
            var endPeriod = endDate?.ToUniversalTime() ?? now;

            var logs = await _context.TelemetryLogs
                .Where(t => deviceIds.Contains(t.DeviceId) && t.Timestamp >= startPeriod && t.Timestamp <= endPeriod)
                .ToListAsync();

            var monthsList = new List<string>();
            var energyValues = new List<double>();
            var gasValues = new List<double>();
            var waterValues = new List<double>();

            for (int i = defaultMonths - 1; i >= 0; i--)
            {
                var targetMonth = now.AddMonths(-i);
                var monthLabel = targetMonth.ToString("MMM").ToUpper();
                monthsList.Add(monthLabel);

                var monthLogs = logs
                    .Where(l => l.Timestamp.Year == targetMonth.Year && l.Timestamp.Month == targetMonth.Month)
                    .ToList();

                var monthEnergy = monthLogs.Sum(l => l.ElectricityReading) * 10;
                var monthGas = monthLogs.Sum(l => l.GasReading) * 2;
                var monthWater = monthLogs.Sum(l => l.WaterReading) * 0.5;

                energyValues.Add(Math.Round(monthEnergy, 0));
                gasValues.Add(Math.Round(monthGas, 0));
                waterValues.Add(Math.Round(monthWater, 0));
            }

            var currentEnergyVal = energyValues[defaultMonths - 1];
            var currentGasVal = gasValues[defaultMonths - 1];
            var currentWaterVal = waterValues[defaultMonths - 1];

            var prevEnergyVal = defaultMonths > 1 ? energyValues[defaultMonths - 2] : 0;
            var prevGasVal = defaultMonths > 1 ? gasValues[defaultMonths - 2] : 0;
            var prevWaterVal = defaultMonths > 1 ? waterValues[defaultMonths - 2] : 0;

            string energyTrend = "+0.0%";
            string energyTrendVariant = "success";
            if (prevEnergyVal > 0)
            {
                var diff = ((currentEnergyVal - prevEnergyVal) / prevEnergyVal) * 100;
                energyTrend = (diff >= 0 ? "+" : "") + Math.Round(diff, 1) + "%";
                energyTrendVariant = diff >= 0 ? "danger" : "success";
            }

            string gasTrend = "+0.0%";
            string gasTrendVariant = "success";
            if (prevGasVal > 0)
            {
                var diff = ((currentGasVal - prevGasVal) / prevGasVal) * 100;
                gasTrend = (diff >= 0 ? "+" : "") + Math.Round(diff, 1) + "%";
                gasTrendVariant = diff >= 0 ? "danger" : "success";
            }

            string waterTrend = "+0.0%";
            string waterTrendVariant = "success";
            if (prevWaterVal > 0)
            {
                var diff = ((currentWaterVal - prevWaterVal) / prevWaterVal) * 100;
                waterTrend = (diff >= 0 ? "+" : "") + Math.Round(diff, 1) + "%";
                waterTrendVariant = diff >= 0 ? "danger" : "success";
            }

            double currentCost = 0;
            if (hasElectricityLinked) currentCost += (currentEnergyVal * 1.5);
            if (hasGasLinked) currentCost += (currentGasVal * 4.0);
            if (hasWaterLinked) currentCost += (currentWaterVal * 2.0);

            var budgetLimit = 15000.0;
            var budgetPercent = (currentCost / budgetLimit) * 100;
            if (budgetPercent > 100) budgetPercent = 100;

            var propertyBreakdowns = new List<PropertyBreakdownDto>();
            foreach (var prop in properties)
            {
                var propDevices = devices.Where(d => d.PropertyId == prop.Id).Select(d => d.Id).ToList();
                var propLogs = logs
                    .Where(l => propDevices.Contains(l.DeviceId) && l.Timestamp.Year == now.Year && l.Timestamp.Month == now.Month)
                    .ToList();

                var propEnergy = Math.Round(propLogs.Sum(l => l.ElectricityReading) * 10, 0);
                var propGas = Math.Round(propLogs.Sum(l => l.GasReading) * 2, 0);
                var propWater = Math.Round(propLogs.Sum(l => l.WaterReading) * 0.5, 0);

                string status = "optimal";
                if (propEnergy > 1500 || propGas > 400 || propWater > 300)
                    status = "high-load";
                else if (propEnergy > 800 || propGas > 200 || propWater > 150)
                    status = "monitor";

                propertyBreakdowns.Add(new PropertyBreakdownDto(
                    prop.Id,
                    prop.Name,
                    $"{prop.City}, {prop.Country}",
                    propEnergy,
                    propGas,
                    propWater,
                    status
                ));
            }

            return Ok(new ConsumptionSummaryResponseDto(
                hasElectricityLinked,
                hasGasLinked,
                hasWaterLinked,
                new ConsumptionMetricsDto(
                    new ConsumptionMetricDto($"{currentEnergyVal:N0}", "kWh", energyTrend, energyTrendVariant),
                    new ConsumptionMetricDto($"{currentGasVal:N0}", "m³", gasTrend, gasTrendVariant),
                    new ConsumptionMetricDto($"{currentWaterVal:N0}", "m³", waterTrend, waterTrendVariant),
                    new ProjectedCostsDto($"{currentCost:N2}", $"{budgetLimit:N0}", Math.Round(budgetPercent, 0))
                ),
                new ChartDataDto(monthsList, energyValues, gasValues, waterValues),
                propertyBreakdowns
            ));
        }

        private async Task<IActionResult> HandleExportAsync(Landlord landlord, long userId, int months, string format)
        {
            if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                var subscription = await _context.Subscriptions
                    .Include(s => s.Plan)
                    .FirstOrDefaultAsync(s => s.LandlordId == landlord.Id);

                if (subscription != null && subscription.Plan.Name.Equals("Basic", StringComparison.OrdinalIgnoreCase))
                {
                    if (months > 3) months = 3;
                }

                try
                {
                    var fileBytes = await _reportService.GenerateConsumptionPdfReportAsync(userId, months);
                    var fileName = $"consumption_report_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                    return File(fileBytes, "application/pdf", fileName);
                }
                catch (Exception)
                {
                    return StatusCode(500, new ErrorResponse("InternalServerError", _localizer["Internal_ServerError"]));
                }
            }

            return BadRequest(new ErrorResponse("BadRequest", _localizer["Report_UnsupportedFormat"]));
        }
    }
}
