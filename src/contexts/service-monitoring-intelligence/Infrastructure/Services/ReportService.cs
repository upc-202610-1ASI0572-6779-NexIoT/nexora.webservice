using System;
using System.Collections.Generic;
<<<<<<< HEAD
using System.Globalization;
=======
>>>>>>> feature/report-analytics
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Dto;
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

        // ---------------------------------------------------------------------
        // Consumption analytics for the mobile Reports module
        // ---------------------------------------------------------------------

        // Nominal mains voltage used to derive instantaneous power (kW) from the
        // current (A) reported by the electrical sensor. Peru runs on ~220 V.
        private const double NominalVoltage = 220.0;
        // Safe operating thresholds mirror the embedded firmware limits.
        private const double WaterSafeFlowLpm = 20.0;
        private const double ElectricalSafeCurrentA = 20.0;

        public async Task<ConsumptionReportDto> GetConsumptionReportAsync(string metric, string range, string? deviceId = null)
        {
            metric = (metric ?? "water").Trim().ToLowerInvariant();
            if (metric != "electricity") metric = "water";
            range = (range ?? "week").Trim().ToLowerInvariant();
            bool isWater = metric == "water";

            // Resolve bucketing for the requested range.
            int bucketCount;
            TimeSpan bucketSize;
            string averageLabel;
            switch (range)
            {
                case "day":   bucketCount = 24; bucketSize = TimeSpan.FromHours(1); averageLabel = "per hour";  break;
                case "month": bucketCount = 30; bucketSize = TimeSpan.FromDays(1);  averageLabel = "per day";   break;
                case "year":  bucketCount = 12; bucketSize = TimeSpan.FromDays(30); averageLabel = "per month"; break;
                default:      range = "week"; bucketCount = 7; bucketSize = TimeSpan.FromDays(1); averageLabel = "per day"; break;
            }
            var window = TimeSpan.FromTicks(bucketSize.Ticks * bucketCount);

            var dto = new ConsumptionReportDto
            {
                Metric = metric,
                Range = range,
                Unit = isWater ? "L" : "kWh",
                RateUnit = isWater ? "L/min" : "kW",
                AverageLabel = averageLabel,
                SafeThreshold = Math.Round(isWater ? WaterSafeFlowLpm : (ElectricalSafeCurrentA * NominalVoltage / 1000.0), 2),
                HasData = false,
            };

            var baseQuery = _context.TelemetryLogs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(deviceId))
                baseQuery = baseQuery.Where(t => t.DeviceId == deviceId);

            // Anchor the window to the most recent reading so the report always shows the
            // latest available data (live when the edge is feeding, else the last batch).
            var lastReadingAt = await baseQuery.Select(t => (DateTime?)t.Timestamp).MaxAsync();
            if (lastReadingAt == null)
            {
                dto.Series = Enumerable.Repeat(0.0, bucketCount).ToList();
                dto.AxisLabels = BuildAxisLabels(range, DateTime.UtcNow - window, DateTime.UtcNow);
                return dto;
            }

            var end = lastReadingAt.Value;
            var start = end - window;
            var prevStart = start - window;

            // We can only compare against the previous period if our history reaches
            // back to (at least) the start of that period.
            var firstReadingAt = await baseQuery.Select(t => (DateTime?)t.Timestamp).MinAsync();
            bool comparable = firstReadingAt != null && firstReadingAt.Value <= prevStart + bucketSize;

            var rows = await baseQuery
                .Where(t => t.Timestamp > prevStart && t.Timestamp <= end)
                .Select(t => new TelemetrySample
                {
                    DeviceId = t.DeviceId,
                    Timestamp = t.Timestamp,
                    Reading = isWater ? t.WaterReading : t.ElectricityReading
                })
                .ToListAsync();

            // Instantaneous reading -> consumption contributed over one bucket.
            double ToConsumption(double avgReading) => isWater
                ? avgReading * bucketSize.TotalMinutes                          // L/min * min = L
                : avgReading * (NominalVoltage / 1000.0) * bucketSize.TotalHours; // A -> kW -> kWh
            // Instantaneous reading -> display rate (L/min or kW).
            double ToRate(double reading) => isWater ? reading : reading * (NominalVoltage / 1000.0);

            // Per-device, per-bucket consumption so a device that does not measure
            // this metric (reporting 0) never dilutes the ones that do. Total is the
            // sum across devices.
            double[] BucketConsumption(IEnumerable<TelemetrySample> samples, DateTime winStart)
            {
                var arr = new double[bucketCount];
                var list = samples.ToList();
                for (int i = 0; i < bucketCount; i++)
                {
                    var b0 = winStart + TimeSpan.FromTicks(bucketSize.Ticks * i);
                    var b1 = b0 + bucketSize;
                    var inBucket = list.Where(s => s.Timestamp > b0 && s.Timestamp <= b1).Select(s => s.Reading).ToList();
                    arr[i] = inBucket.Count > 0 ? ToConsumption(inBucket.Average()) : 0.0;
                }
                return arr;
            }

            var current = rows.Where(r => r.Timestamp > start && r.Timestamp <= end).ToList();
            var previous = rows.Where(r => r.Timestamp > prevStart && r.Timestamp <= start).ToList();

            // Current period: combined series + per-device totals.
            var series = new double[bucketCount];
            var sources = new List<ConsumptionSourceDto>();
            foreach (var g in current.GroupBy(r => r.DeviceId))
            {
                var devSeries = BucketConsumption(g, start);
                for (int i = 0; i < bucketCount; i++) series[i] += devSeries[i];
                sources.Add(new ConsumptionSourceDto
                {
                    DeviceId = g.Key,
                    Label = FriendlyDeviceLabel(g.Key),
                    Value = Math.Round(devSeries.Sum(), 2),
                });
            }
            double total = series.Sum();

            // Previous period total (for the delta).
            double previousTotal = 0.0;
            foreach (var g in previous.GroupBy(r => r.DeviceId))
                previousTotal += BucketConsumption(g, prevStart).Sum();

            // Peak instantaneous rate within the current window.
            double peak = 0; DateTime? peakAt = null;
            foreach (var r in current)
            {
                var rate = ToRate(r.Reading);
                if (rate > peak) { peak = rate; peakAt = r.Timestamp; }
            }

            // Finalise source shares (drop sources that contributed nothing).
            sources = sources.Where(s => s.Value > 0.01).OrderByDescending(s => s.Value).ToList();
            foreach (var s in sources)
                s.SharePercent = total > 0 ? Math.Round(s.Value / total * 100.0, 1) : 0.0;

            double deltaPercent;
            if (previousTotal > 0.01) deltaPercent = (total - previousTotal) / previousTotal * 100.0;
            else deltaPercent = total > 0 ? 100.0 : 0.0;

            dto.Series = series.Select(v => Math.Round(v, 2)).ToList();
            dto.AxisLabels = BuildAxisLabels(range, start, end);
            dto.Total = Math.Round(total, 2);
            dto.PreviousTotal = Math.Round(previousTotal, 2);
            dto.DeltaPercent = Math.Round(deltaPercent, 1);
            dto.Increase = total >= previousTotal;
            dto.Comparable = comparable;
            dto.Average = Math.Round(total / bucketCount, 2);
            dto.Peak = Math.Round(peak, 2);
            dto.PeakAt = peakAt;
            dto.HighUsage = peak >= dto.SafeThreshold && peak > 0;
            dto.Sources = sources;
            dto.SampleCount = current.Count;
            dto.LastReadingAt = end;
            dto.HasData = current.Count > 0;
            return dto;
        }

        private sealed class TelemetrySample
        {
            public string DeviceId { get; set; } = null!;
            public DateTime Timestamp { get; set; }
            public double Reading { get; set; }
        }

        private static List<string> BuildAxisLabels(string range, DateTime start, DateTime end)
        {
            const int n = 5;
            var labels = new List<string>();
            var span = end - start;
            for (int i = 0; i < n; i++)
            {
                var t = start + TimeSpan.FromTicks(span.Ticks * i / (n - 1));
                labels.Add(FormatAxis(range, t));
            }
            return labels;
        }

        private static string FormatAxis(string range, DateTime t)
        {
            var ci = CultureInfo.InvariantCulture;
            switch (range)
            {
                case "day":
                    int h12 = t.Hour % 12; if (h12 == 0) h12 = 12;
                    return $"{h12}{(t.Hour < 12 ? "a" : "p")}";
                case "month":
                    return t.ToString("M/d", ci);
                case "year":
                    return t.ToString("MMM", ci);
                default: // week
                    return t.ToString("ddd", ci);
            }
        }

        private static string FriendlyDeviceLabel(string deviceId)
        {
            var id = (deviceId ?? string.Empty).ToLowerInvariant();
            if (id.Contains("water")) return "Water line";
            if (id.Contains("volt") || id.Contains("power") || id.Contains("elect") || id.Contains("current"))
                return "Electrical panel";
            if (id.Contains("gas")) return "Gas unit";
            return string.IsNullOrWhiteSpace(deviceId) ? "Unknown device" : deviceId;
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
