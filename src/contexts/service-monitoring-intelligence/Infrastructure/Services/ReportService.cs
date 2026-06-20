using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Services;
using Nexora.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;

namespace Nexora.Infrastructure.Services
{
    public class ReportService : IReportService
    {
        private readonly NexoraDbContext _context;

        static ReportService()
        {
            // Configure free QuestPDF community license
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public ReportService(NexoraDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> GenerateTelemetryPdfReportAsync(string deviceId, DateTime startDate, DateTime endDate)
        {
            var logs = await _context.TelemetryLogs
                .Where(t => t.DeviceId == deviceId && t.Timestamp >= startDate && t.Timestamp <= endDate)
                .OrderBy(t => t.Timestamp)
                .ToListAsync();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Text($"Reporte de Telemetria - Dispositivo: {deviceId}")
                        .SemiBold().FontSize(18).FontColor(Colors.Blue.Darken2);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Timestamp
                                columns.RelativeColumn(2); // Water Reading
                                columns.RelativeColumn(2); // Gas Reading
                                columns.RelativeColumn(2); // Presence
                                columns.RelativeColumn(2); // Electricity
                                columns.RelativeColumn(2); // Voltage Status
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Fecha / Hora").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Agua (Lpm)").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Gas (Ppm)").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Presencia").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Corriente (A)").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Red Electr.").SemiBold();
                            });

                            foreach (var log in logs)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(log.WaterReading.ToString("0.##"));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(log.GasReading.ToString("0.##"));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(log.PresenceReading ? "Si" : "No");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(log.ElectricityReading.ToString("0.##") + " A");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(log.VoltageOk ? "Estable" : "Inestable");
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Pagina ");
                            x.CurrentPageNumber();
                        });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> GenerateTelemetryExcelReportAsync(string deviceId, DateTime startDate, DateTime endDate)
        {
            var logs = await _context.TelemetryLogs
                .Where(t => t.DeviceId == deviceId && t.Timestamp >= startDate && t.Timestamp <= endDate)
                .OrderBy(t => t.Timestamp)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Telemetria");

            // Header labels
            worksheet.Cell(1, 1).Value = "Fecha / Hora";
            worksheet.Cell(1, 2).Value = "Agua (Lpm)";
            worksheet.Cell(1, 3).Value = "Gas (Ppm)";
            worksheet.Cell(1, 4).Value = "Presencia";
            worksheet.Cell(1, 5).Value = "Corriente (A)";
            worksheet.Cell(1, 6).Value = "Red Electrica";

            // Format Header Row
            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F81BD");
            headerRow.Style.Font.FontColor = XLColor.White;

            int currentRow = 2;
            foreach (var log in logs)
            {
                worksheet.Cell(currentRow, 1).Value = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cell(currentRow, 2).Value = log.WaterReading;
                worksheet.Cell(currentRow, 3).Value = log.GasReading;
                worksheet.Cell(currentRow, 4).Value = log.PresenceReading ? "Si" : "No";
                worksheet.Cell(currentRow, 5).Value = log.ElectricityReading;
                worksheet.Cell(currentRow, 6).Value = log.VoltageOk ? "Estable" : "Inestable";
                currentRow++;
            }

            // Autofit all column widths
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> GenerateAlertsPdfReportAsync(DateTime? startDate, DateTime? endDate)
        {
            DateTime actualStartDate;
            DateTime actualEndDate;

            if (startDate.HasValue && endDate.HasValue)
            {
                actualStartDate = startDate.Value;
                actualEndDate = endDate.Value;
            }
            else
            {
                // Fallback to last 2 hours of active alerts in the DB to avoid timezone issues
                var latestAlertTime = await _context.Alerts.Select(a => (DateTime?)a.Timestamp).MaxAsync();
                actualEndDate = latestAlertTime ?? DateTime.UtcNow;
                actualStartDate = actualEndDate.AddHours(-2);
            }

            var alerts = await _context.Alerts
                .Where(a => a.Timestamp >= actualStartDate && a.Timestamp <= actualEndDate)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Text("Incident Report - Emergency Alerts Center")
                        .SemiBold().FontSize(18).FontColor(Colors.Red.Darken2);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1); // ID
                                columns.RelativeColumn(3); // Date/Time
                                columns.RelativeColumn(2); // Severity
                                columns.RelativeColumn(3); // Device ID
                                columns.RelativeColumn(4); // Alert Type
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("ID").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Date / Time").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Severity").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Device ID").SemiBold();
                                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("Alert Type").SemiBold();
                            });

                            foreach (var alert in alerts)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(alert.Id.ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(alert.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                                
                                var severityText = alert.Severity.ToString();
                                var cell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5);
                                if (alert.Severity == AlertSeverity.Critical)
                                {
                                    cell.Text(severityText).Bold().FontColor(Colors.Red.Medium);
                                }
                                else
                                {
                                    cell.Text(severityText).FontColor(Colors.Orange.Medium);
                                }
                                
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(alert.DeviceId);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(alert.Type);
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return stream.ToArray();
        }
    }
}
