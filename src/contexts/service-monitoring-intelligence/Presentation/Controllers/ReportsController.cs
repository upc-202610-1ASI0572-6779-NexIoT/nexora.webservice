using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Nexora.Application.Services;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>
        /// Returns an aggregated, chart-ready consumption report for the mobile Reports
        /// module. Aggregates real telemetry into a trend series, totals, peak, the
        /// change vs the previous period and a per-device breakdown.
        /// </summary>
        [HttpGet("consumption")]
        public async Task<IActionResult> GetConsumption(
            [FromQuery] string metric = "water",
            [FromQuery] string range = "week",
            [FromQuery] string? deviceId = null)
        {
            try
            {
                var report = await _reportService.GetConsumptionReportAsync(metric, range, deviceId);
                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error building consumption report: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportReport(
            [FromQuery] string deviceId, 
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate,
            [FromQuery] string format)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return BadRequest("DeviceId is required.");
            }

            if (startDate > endDate)
            {
                return BadRequest("StartDate cannot be greater than EndDate.");
            }

            if (string.IsNullOrWhiteSpace(format))
            {
                return BadRequest("Format is required (pdf or xlsx).");
            }

            var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

            if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var fileBytes = await _reportService.GenerateTelemetryPdfReportAsync(deviceId, utcStartDate, utcEndDate);
                    var fileName = $"telemetry_report_{deviceId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                    return File(fileBytes, "application/pdf", fileName);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error generating PDF report: {ex.Message}");
                }
            }
            else if (format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) || format.Equals("excel", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var fileBytes = await _reportService.GenerateTelemetryExcelReportAsync(deviceId, utcStartDate, utcEndDate);
                    var fileName = $"telemetry_report_{deviceId}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error generating Excel report: {ex.Message}");
                }
            }
            else
            {
                return BadRequest("Unsupported report format. Supported formats are: pdf, xlsx.");
            }
        }

        [HttpGet("/api/v1/alerts/reports")]
        public async Task<IActionResult> ExportAlertsReport(
            [FromQuery] DateTime? startDate, 
            [FromQuery] DateTime? endDate,
            [FromQuery] string format = "pdf")
        {
            if (string.IsNullOrWhiteSpace(format) || !format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Unsupported report format. Supported format is: pdf.");
            }

            var utcStartDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : (DateTime?)null;
            var utcEndDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : (DateTime?)null;

            try
            {
                var fileBytes = await _reportService.GenerateAlertsPdfReportAsync(utcStartDate, utcEndDate);
                var fileName = $"alerts_report_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error generating Alerts PDF report: {ex.Message}");
            }
        }

        [HttpGet("consumption")]
        [Authorize]
        public async Task<IActionResult> ExportConsumptionReport([FromQuery] int months = 6)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            try
            {
                var fileBytes = await _reportService.GenerateConsumptionPdfReportAsync(userId, months);
                var fileName = $"consumption_report_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error generating consumption report: {ex.Message}");
            }
        }
    }
}
