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
        Task<ConsumptionReportDto> GetConsumptionReportAsync(string metric, string range, string? deviceId = null);
        Task<byte[]> GenerateConsumptionPdfReportAsync(long userId, int months);
    }
}
