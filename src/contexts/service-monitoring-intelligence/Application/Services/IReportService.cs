using System;
using System.Threading.Tasks;
using Nexora.Application.Dto;

namespace Nexora.Application.Services
{
    public interface IReportService
    {
        Task<byte[]> GenerateTelemetryPdfReportAsync(string deviceId, DateTime startDate, DateTime endDate);
        Task<byte[]> GenerateTelemetryExcelReportAsync(string deviceId, DateTime startDate, DateTime endDate);
        Task<byte[]> GenerateAlertsPdfReportAsync(DateTime? startDate, DateTime? endDate);

        /// <summary>
        /// Builds an aggregated, chart-ready consumption report for the Reports module.
        /// </summary>
        /// <param name="metric">"water" or "electricity".</param>
        /// <param name="range">"day", "week", "month" or "year".</param>
        /// <param name="deviceId">Optional device filter; aggregates all devices when null.</param>
        Task<ConsumptionReportDto> GetConsumptionReportAsync(string metric, string range, string? deviceId = null);
    }
}
