using System;
using System.Collections.Generic;
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

        public async Task<byte[]> GenerateConsumptionPdfReportAsync(long userId, int months)
        {
            if (months <= 0) months = 6;

            var landlord = await _context.Landlords
                .FirstOrDefaultAsync(l => l.UserId == userId);
            if (landlord == null) throw new ArgumentException("Landlord profile not found.");

            long landlordId = landlord.Id;

            var properties = await _context.Properties
                .Where(p => p.LandlordId == landlordId)
                .ToListAsync();

            var propertyIds = properties.Select(p => p.Id).ToList();
            var devices = await _context.Devices
                .Where(d => d.PropertyId != null && propertyIds.Contains(d.PropertyId.Value))
                .ToListAsync();

            var deviceIds = devices.Select(d => d.Id).ToList();

            var now = DateTime.UtcNow;
            var startPeriod = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

            var logs = await _context.TelemetryLogs
                .Where(t => deviceIds.Contains(t.DeviceId) && t.Timestamp >= startPeriod)
                .ToListAsync();

            var monthsList = new List<string>();
            var energyValues = new List<double>();
            var gasValues = new List<double>();

            for (int i = months - 1; i >= 0; i--)
            {
                var targetMonth = now.AddMonths(-i);
                var monthLabel = targetMonth.ToString("MMM").ToUpper();
                monthsList.Add(monthLabel);

                var monthLogs = logs
                    .Where(l => l.Timestamp.Year == targetMonth.Year && l.Timestamp.Month == targetMonth.Month)
                    .ToList();

                var monthEnergy = monthLogs.Sum(l => l.ElectricityReading) * 10;
                var monthGas = monthLogs.Sum(l => l.GasReading) * 2;

                energyValues.Add(Math.Round(monthEnergy, 0));
                gasValues.Add(Math.Round(monthGas, 0));
            }

            var currentEnergyVal = energyValues[months - 1];
            var currentGasVal = gasValues[months - 1];
            var currentCost = (currentEnergyVal * 1.5) + (currentGasVal * 4.0);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Text($"Reporte de Consumo Nexora - Landlord ID: {landlordId}")
                        .SemiBold().FontSize(18).FontColor(Colors.Orange.Darken3);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(20);

                            // Section 1: Consumption Summary
                            column.Item().Text("Resumen de Consumo Mensual Activo").SemiBold().FontSize(14);
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1).Padding(5).Text("Energia Total (kWh)").Bold();
                                    header.Cell().BorderBottom(1).Padding(5).Text("Gas Total (m3)").Bold();
                                    header.Cell().BorderBottom(1).Padding(5).Text("Costo Proyectado ($)").Bold();
                                });

                                table.Cell().Padding(5).Text($"{currentEnergyVal:N0}");
                                table.Cell().Padding(5).Text($"{currentGasVal:N0}");
                                table.Cell().Padding(5).Text($"${currentCost:N2}");
                            });

                            // Section 2: Monthly breakdown
                            column.Item().Text("Historial de Consumo por Mes").SemiBold().FontSize(14);
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1).Padding(5).Text("Mes").Bold();
                                    header.Cell().BorderBottom(1).Padding(5).Text("Energia (kWh)").Bold();
                                    header.Cell().BorderBottom(1).Padding(5).Text("Gas (m3)").Bold();
                                });

                                for (int idx = 0; idx < months; idx++)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(monthsList[idx]);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"{energyValues[idx]:N0}");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"{gasValues[idx]:N0}");
                                }
                            });

                            // Section 3: Property breakdown
                            column.Item().Text("Desglose por Propidades").SemiBold().FontSize(14);
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1).Padding(5).Text("Propiedad").Bold();
                                    header.Cell().BorderBottom(1).Padding(5).Text("Ubicacion").Bold();
                                    header.Cell().BorderBottom(1).Padding(5).Text("Energia (kWh)").Bold();
                                    header.Cell().BorderBottom(1).Padding(5).Text("Gas (m3)").Bold();
                                    header.Cell().BorderBottom(1).Padding(5).Text("Estado").Bold();
                                });

                                foreach (var prop in properties)
                                {
                                    var propDevices = devices.Where(d => d.PropertyId == prop.Id).Select(d => d.Id).ToList();
                                    var propLogs = logs
                                        .Where(l => propDevices.Contains(l.DeviceId) && l.Timestamp.Year == now.Year && l.Timestamp.Month == now.Month)
                                        .ToList();

                                    var propEnergy = Math.Round(propLogs.Sum(l => l.ElectricityReading) * 10, 0);
                                    var propGas = Math.Round(propLogs.Sum(l => l.GasReading) * 2, 0);

                                    string status = "optimal";
                                    if (propEnergy > 1500 || propGas > 400)
                                    {
                                        status = "high-load";
                                    }
                                    else if (propEnergy > 800 || propGas > 200)
                                    {
                                        status = "monitor";
                                    }

                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(prop.Name);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"{prop.City}, {prop.Country}");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"{propEnergy:N0}");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"{propGas:N0}");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(status.ToUpper());
                                }
                            });
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
    }
}
