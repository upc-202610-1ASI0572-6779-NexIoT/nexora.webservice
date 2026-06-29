using System;
using System.Threading.Tasks;

namespace Nexora.Application.Services
{
    public interface IReportService
    {
        Task<byte[]> GenerateTelemetryPdfReportAsync(string deviceId, DateTime startDate, DateTime endDate);
        Task<byte[]> GenerateTelemetryExcelReportAsync(string deviceId, DateTime startDate, DateTime endDate);
        Task<byte[]> GenerateAlertsPdfReportAsync(DateTime? startDate, DateTime? endDate);
        Task<byte[]> GenerateConsumptionPdfReportAsync(long userId, int months);
    }
}
