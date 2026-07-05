using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Enums;
using Nexora.Domain.Entities;
using Nexora.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/analytics")]
    [Authorize]
    public class AnalyticsController : ControllerBase
    {
        private readonly NexoraDbContext _context;

        public AnalyticsController(NexoraDbContext context)
        {
            _context = context;
        }

        [HttpGet("consumption-summary")]
        public async Task<IActionResult> GetConsumptionSummary([FromQuery] int months = 6)
        {
            if (months <= 0) months = 6;

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var landlord = await _context.Landlords
                .FirstOrDefaultAsync(l => l.UserId == userId);
            if (landlord == null) return NotFound("Landlord profile not found.");

            var properties = await _context.Properties
                .Where(p => p.LandlordId == landlord.Id)
                .ToListAsync();

            var propertyIds = properties.Select(p => p.Id).ToList();
            var devices = await _context.Devices
                .Where(d => d.PropertyId != null && propertyIds.Contains(d.PropertyId.Value))
                .ToListAsync();

            var deviceIds = devices.Select(d => d.Id).ToList();

            var hasElectricityLinked = devices.Any(d => d.Id.ToLower().Contains("voltage") || d.Id.ToLower().Contains("electricity"));
            var hasGasLinked = devices.Any(d => d.Id.ToLower().Contains("gas"));
            var hasWaterLinked = devices.Any(d => d.Id.ToLower().Contains("water") || d.Id.ToLower().Contains("agua"));

            var now = DateTime.UtcNow;
            var startPeriod = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

            var logs = await _context.TelemetryLogs
                .Where(t => deviceIds.Contains(t.DeviceId) && t.Timestamp >= startPeriod)
                .ToListAsync();

            var monthsList = new List<string>();
            var energyValues = new List<double>();
            var gasValues = new List<double>();
            var waterValues = new List<double>();

            for (int i = months - 1; i >= 0; i--)
            {
                var targetMonth = now.AddMonths(-i);
                var monthLabel = targetMonth.ToString("MMM").ToUpper();
                monthsList.Add(monthLabel);

                var monthLogs = logs
                    .Where(l => l.Timestamp.Year == targetMonth.Year && l.Timestamp.Month == targetMonth.Month)
                    .ToList();

                // Compute aggregated proxies
                var monthEnergy = monthLogs.Sum(l => l.ElectricityReading) * 10;
                var monthGas = monthLogs.Sum(l => l.GasReading) * 2;
                var monthWater = monthLogs.Sum(l => l.WaterReading) * 0.5;

                energyValues.Add(Math.Round(monthEnergy, 0));
                gasValues.Add(Math.Round(monthGas, 0));
                waterValues.Add(Math.Round(monthWater, 0));
            }

            var currentEnergyVal = energyValues[months - 1];
            var currentGasVal = gasValues[months - 1];
            var currentWaterVal = waterValues[months - 1];

            var prevEnergyVal = months > 1 ? energyValues[months - 2] : 0;
            var prevGasVal = months > 1 ? gasValues[months - 2] : 0;
            var prevWaterVal = months > 1 ? waterValues[months - 2] : 0;

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

            // Estimate cost: based only on connected services
            double currentCost = 0;
            if (hasElectricityLinked) currentCost += (currentEnergyVal * 1.5);
            if (hasGasLinked) currentCost += (currentGasVal * 4.0);
            if (hasWaterLinked) currentCost += (currentWaterVal * 2.0);

            var budgetLimit = 15000.0;
            var budgetPercent = (currentCost / budgetLimit) * 100;
            if (budgetPercent > 100) budgetPercent = 100;

            var propertyBreakdowns = new List<object>();
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
                {
                    status = "high-load";
                }
                else if (propEnergy > 800 || propGas > 200 || propWater > 150)
                {
                    status = "monitor";
                }

                propertyBreakdowns.Add(new
                {
                    id = prop.Id,
                    name = prop.Name,
                    location = $"{prop.City}, {prop.Country}",
                    energy = propEnergy,
                    gas = propGas,
                    water = propWater,
                    status = status
                });
            }

            return Ok(new
            {
                hasElectricityLinked,
                hasGasLinked,
                hasWaterLinked,
                consumption = new
                {
                    energy = new { value = $"{currentEnergyVal:N0}", unit = "kWh", trend = energyTrend, trendVariant = energyTrendVariant },
                    gas = new { value = $"{currentGasVal:N0}", unit = "m³", trend = gasTrend, trendVariant = gasTrendVariant },
                    water = new { value = $"{currentWaterVal:N0}", unit = "m³", trend = waterTrend, trendVariant = waterTrendVariant },
                    projectedCosts = new { value = $"{currentCost:N2}", budgetLimit = $"{budgetLimit:N0}", budgetPercent = Math.Round(budgetPercent, 0) }
                },
                chartData = new
                {
                    months = monthsList,
                    energy = energyValues,
                    gas = gasValues,
                    water = waterValues
                },
                propertyBreakdown = propertyBreakdowns
            });
        }

        [HttpGet("live-consumption")]
        public async Task<IActionResult> GetLiveConsumption([FromQuery] string range = "24h")
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var landlord = await _context.Landlords.FirstOrDefaultAsync(l => l.UserId == userId);
            if (landlord == null) return NotFound("Landlord profile not found.");

            var propertyIds = await _context.Properties
                .Where(p => p.LandlordId == landlord.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var deviceIds = await _context.Devices
                .Where(d => d.PropertyId != null && propertyIds.Contains(d.PropertyId.Value))
                .Select(d => d.Id)
                .ToListAsync();

            var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);
            var labels = new List<string>();
            var gasData = new List<double>();
            var electricityData = new List<double>();
            var waterData = new List<double>();

            if (range == "7d")
            {
                // Last 7 days
                for (int i = 6; i >= 0; i--)
                {
                    var dateLocal = nowLocal.AddDays(-i).Date;
                    labels.Add(dateLocal.ToString("ddd"));

                    var dateUtcStart = TimeZoneInfo.ConvertTimeToUtc(dateLocal, userTimeZone);
                    var dateUtcEnd = dateUtcStart.AddDays(1);

                    var dayLogs = await _context.TelemetryLogs
                        .Where(t => deviceIds.Contains(t.DeviceId) && t.Timestamp >= dateUtcStart && t.Timestamp < dateUtcEnd)
                        .ToListAsync();

                    gasData.Add(dayLogs.Any() ? Math.Round(dayLogs.Average(t => t.GasReading), 1) : 0);
                    electricityData.Add(dayLogs.Any() ? Math.Round(dayLogs.Average(t => t.ElectricityReading), 1) : 0);
                    waterData.Add(dayLogs.Any() ? Math.Round(dayLogs.Average(t => t.WaterReading), 1) : 0);
                }
            }
            else
            {
                // Last 24 hours (grouped in 8 intervals of 3 hours)
                for (int i = 7; i >= 0; i--)
                {
                    var timeLocal = nowLocal.AddHours(-i * 3);
                    labels.Add(timeLocal.ToString("h tt"));

                    var startTimeLocal = timeLocal.AddHours(-3);
                    var endTimeLocal = timeLocal;

                    var startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(startTimeLocal, userTimeZone);
                    var endTimeUtc = TimeZoneInfo.ConvertTimeToUtc(endTimeLocal, userTimeZone);

                    var intervalLogs = await _context.TelemetryLogs
                        .Where(t => deviceIds.Contains(t.DeviceId) && t.Timestamp > startTimeUtc && t.Timestamp <= endTimeUtc)
                        .ToListAsync();

                    gasData.Add(intervalLogs.Any() ? Math.Round(intervalLogs.Average(t => t.GasReading), 1) : 0);
                    electricityData.Add(intervalLogs.Any() ? Math.Round(intervalLogs.Average(t => t.ElectricityReading), 1) : 0);
                    waterData.Add(intervalLogs.Any() ? Math.Round(intervalLogs.Average(t => t.WaterReading), 1) : 0);
                }
            }

            return Ok(new
            {
                labels,
                gas = gasData,
                electricity = electricityData,
                water = waterData
            });
        }
    }
}
