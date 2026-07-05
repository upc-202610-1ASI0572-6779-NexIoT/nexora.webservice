using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Nexora.Application.Commands.Property;
using Nexora.Application.Dto;
using Nexora.Application.Services;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Infrastructure.Persistence;

namespace Nexora.WebApi.Seeding
{
    public class DataSeeder
    {
        private readonly NexoraDbContext _context;
        private readonly IAuthService _authService;
        private readonly IMediator _mediator;

        public DataSeeder(NexoraDbContext context, IAuthService authService, IMediator mediator)
        {
            _context = context;
            _authService = authService;
            _mediator = mediator;
        }

        public async Task EnsureSeedDataAsync()
        {
            if (await _context.Users.AnyAsync()) return;

            await SeedLandlordUsersAsync();
            await SeedPropertiesAsync();
            await SeedGuestTenantsAsync();
            await SeedTenantUsersAsync();
            await SeedSubscriptionsDataAsync();
            await SeedNotificationPreferencesAsync();
            await SeedIoTDataAsync();
        }

        private async Task SeedLandlordUsersAsync()
        {
            var landlords = new[]
            {
                new RegisterLandlordDto("test@example.com", "Nexora2026!", "Juan", "Pérez", "México", "Ciudad de México", "Av. Insurgentes Sur 123, Col. Condesa", "+525512345678"),
                new RegisterLandlordDto("jh_slin@nexora.com", "root", "Jhosep", "Argomedo", "Perú", "Lima", "Av. P.º de la República 615, Miraflores", "+51978777386"),
                new RegisterLandlordDto("developer@nexora.com", "root", "Dev", "Developer", "Perú", "Lima", "Av. Javier Prado 100, San Isidro", "+51999999999"),
                new RegisterLandlordDto("mario.pinedo@gmail.com", "Nexora2026!", "Mario", "Pinedo", "Perú", "Lima", "Av. La Molina 2550, La Molina", "+51987654321"),
                new RegisterLandlordDto("sebasram@nexora.com", "Nexora2026!", "Sebastián", "Ramírez", "Argentina", "Resistencia", "Av. Chaco 743, Centro", "+54936083234")
            };

            foreach (var u in landlords)
            {
                await _authService.RegisterLandlordAsync(u);
            }
        }

        private async Task SeedPropertiesAsync()
        {
            var properties = new[]
            {
                (Name: "Departamento Miraflores", Description: "Departamento moderno con vista al mar en Miraflores", Type: PropertyType.APARTMENT, Country: "Perú", City: "Lima", Address: "Malecón Cisneros 250, Miraflores", OwnerEmail: "jh_slin@nexora.com"),
                (Name: "Local Comercial Centro", Description: "Local en zona de alto tránsito peatonal", Type: PropertyType.COMMERCIAL, Country: "Perú", City: "Lima", Address: "Jirón de la Unión 400, Centro Histórico", OwnerEmail: "jh_slin@nexora.com"),
                (Name: "Casa Magdalena", Description: "Casa familiar en el corazón de Magdalena", Type: PropertyType.HOUSE, Country: "Perú", City: "Lima", Address: "Jr. Echenique 215, Magdalena del Mar", OwnerEmail: "jh_slin@nexora.com"),
                (Name: "Oficinas San Isidro", Description: "Oficina corporativa en el distrito financiero", Type: PropertyType.OFFICE, Country: "Perú", City: "Lima", Address: "Av. San Borja Sur 600, San Isidro", OwnerEmail: "developer@nexora.com"),
                (Name: "Galpón Industrial Ate", Description: "Galpón industrial para logística y almacenamiento", Type: PropertyType.COMMERCIAL, Country: "Perú", City: "Lima", Address: "Av. Industrial 400, Ate", OwnerEmail: "developer@nexora.com"),
                (Name: "Condesa Luxury Apartment", Description: "Departamento de lujo en La Condesa", Type: PropertyType.APARTMENT, Country: "México", City: "Ciudad de México", Address: "Av. Ámsterdam 245, Col. Condesa", OwnerEmail: "test@example.com"),
                (Name: "Casa de Playa Miraflores", Description: "Casa con vista al océano Pacífico", Type: PropertyType.HOUSE, Country: "Perú", City: "Lima", Address: "Malecón Balta 120, Miraflores", OwnerEmail: "mario.pinedo@gmail.com"),
                (Name: "Oficinas Resistencia Centro", Description: "Oficina céntrica en Resistencia", Type: PropertyType.OFFICE, Country: "Argentina", City: "Resistencia", Address: "Av. Alvear 350, Centro", OwnerEmail: "sebasram@nexora.com")
            };

            foreach (var p in properties)
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == p.OwnerEmail);
                if (user == null) continue;

                var cmd = new CreatePropertyCommand(p.Name, p.Description, p.Type, p.Country, p.City, p.Address, IsSecurityModeArmed: false, user.Id);
                await _mediator.Send(cmd);
            }

            await _context.SaveChangesAsync();
        }

        private async Task SeedGuestTenantsAsync()
        {
            if (await _context.Tenants.AnyAsync()) return;

            var properties = await _context.Properties.OrderBy(p => p.Id).ToListAsync();
            if (properties.Count < 8) return;

            var guestsByProperty = new Dictionary<int, (string FirstName, string LastName, string Country, string City, string Address, string Phone)[]>
            {
                { 0, new[] { ("Carlos", "García", "Perú", "Lima", "Av. Larco 123, Miraflores", "+51999111001") } },
                { 1, new[] {
                    ("María", "López", "Perú", "Lima", "Jr. Unión 456, Centro", "+51999111002"),
                    ("José", "Martínez", "Perú", "Lima", "Av. Abancay 789, Centro", "+51999111003")
                }},
                { 2, new[] { ("Luis", "Fernández", "Perú", "Lima", "Av. Primavera 111, Magdalena", "+51999111004") } },
                { 5, new[] {
                    ("Carmen", "Torres", "México", "Ciudad de México", "Av. Michoacán 50, Col. Condesa", "+525598761001"),
                    ("Pedro", "Sánchez", "México", "Ciudad de México", "Calle Durango 200, Col. Roma", "+525598761002")
                }},
                { 6, new[] { ("Rosa", "Ramírez", "Perú", "Lima", "Malecón Balta 333, Miraflores", "+51999111005") } }
            };

            foreach (var (index, guests) in guestsByProperty)
            {
                if (index >= properties.Count) continue;
                var property = properties[index];
                foreach (var g in guests)
                {
                    var tenant = new Tenant(property.Id, g.FirstName, g.LastName, g.Country, g.City, g.Address, g.Phone);
                    _context.Tenants.Add(tenant);
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task SeedTenantUsersAsync()
        {
            var properties = await _context.Properties.OrderBy(p => p.Id).ToListAsync();
            if (properties.Count < 8) return;

            var tenantUsers = new[]
            {
                new RegisterTenantDto("srt0808@nexora.com", "Nexora2026!", "Sara", "Torres", "Perú", "Lima", "Av. Javier Prado 210, San Isidro", "+51987654001", properties[0].Id),
                new RegisterTenantDto("ana.rodriguez@nexora.com", "Nexora2026!", "Ana", "Rodríguez", "Perú", "Lima", "Jr. Carabaya 350, Centro", "+51987654002", properties[1].Id),
                new RegisterTenantDto("carlos.lopez@nexora.com", "Nexora2026!", "Carlos", "López", "México", "Ciudad de México", "Av. Sonora 180, Col. Condesa", "+525598765432", properties[5].Id)
            };

            foreach (var t in tenantUsers)
            {
                await _authService.RegisterTenantAsync(t);
            }
        }

        private async Task SeedSubscriptionsDataAsync()
        {
            var professionalPlan = await _context.SubscriptionPlans.FindAsync(2L);
            var basicPlan = await _context.SubscriptionPlans.FindAsync(1L);
            if (professionalPlan == null || basicPlan == null) return;

            var now = DateTime.UtcNow;
            var sixMonthsAgo = now.AddMonths(-6);
            var oneMonthAgo = now.AddMonths(-1);
            var oneMonthFromNow = now.AddMonths(1);

            var landlordConfigs = new[]
            {
                (Email: "jh_slin@nexora.com", Plan: professionalPlan, UpgradeAt: sixMonthsAgo.AddMonths(3)),
                (Email: "developer@nexora.com", Plan: professionalPlan, UpgradeAt: (DateTime?)null)
            };

            foreach (var cfg in landlordConfigs)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == cfg.Email);
                if (user == null) continue;

                var landlord = await _context.Landlords.FirstOrDefaultAsync(l => l.UserId == user.Id);
                if (landlord == null) continue;

                var subscription = await _context.Subscriptions
                    .Include(s => s.Invoices)
                    .FirstOrDefaultAsync(s => s.LandlordId == landlord.Id);

                if (subscription == null)
                {
                    subscription = new Subscription(landlord.Id, basicPlan.Id, sixMonthsAgo, sixMonthsAgo.AddMonths(1));
                    _context.Subscriptions.Add(subscription);
                    await _context.SaveChangesAsync();
                }

                var subId = subscription.Id;
                var planId = cfg.Plan.Id;

                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE subscriptions SET started_at = {0}, current_period_start = {1}, current_period_end = {2}, subscription_plan_id = {3} WHERE id = {4}",
                    sixMonthsAgo, oneMonthAgo, oneMonthFromNow, planId, subId
                );

                var monthlyPrice = cfg.Plan.MonthlyPrice;
                var monthlyAmounts = new[] {
                    basicPlan.MonthlyPrice,
                    basicPlan.MonthlyPrice,
                    basicPlan.MonthlyPrice,
                    monthlyPrice,
                    monthlyPrice,
                    monthlyPrice
                };

                var existingInvoices = await _context.Invoices
                    .Where(i => i.SubscriptionId == subId)
                    .OrderBy(i => i.Id)
                    .ToListAsync();

                if (existingInvoices.Count == 0)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        var dueDate = sixMonthsAgo.AddMonths(i).AddDays(7);
                        var inv = new Invoice(subId, monthlyAmounts[i], dueDate);
                        _context.Invoices.Add(inv);
                    }

                    await _context.SaveChangesAsync();
                    existingInvoices = await _context.Invoices
                        .Where(i => i.SubscriptionId == subId)
                        .OrderBy(i => i.Id)
                        .ToListAsync();
                }

                for (int i = 0; i < existingInvoices.Count && i < 6; i++)
                {
                    var inv = existingInvoices[i];
                    var invCreatedAt = sixMonthsAgo.AddMonths(i).AddDays(1);
                    var dueDate = sixMonthsAgo.AddMonths(i).AddDays(7);

                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE invoices SET created_at = {0}, status = {1}, amount = {2}, due_date = {3} WHERE id = {4}",
                        invCreatedAt, "Paid", monthlyAmounts[i], dueDate, inv.Id
                    );
                }

                if (existingInvoices.Count > 6)
                {
                    foreach (var extraId in existingInvoices.Skip(6).Select(i => i.Id).ToList())
                    {
                        await _context.Database.ExecuteSqlRawAsync("DELETE FROM payments WHERE invoice_id = {0}", extraId);
                        await _context.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE id = {0}", extraId);
                    }
                }

                var paidInvoices = existingInvoices.Take(6).ToList();
                foreach (var inv in paidInvoices)
                {
                    var hasPayment = await _context.Payments.AnyAsync(p => p.InvoiceId == inv.Id);
                    if (!hasPayment)
                    {
                        var payment = new Payment(inv.Id, inv.Amount, "stripe", $"pi_3R{Guid.NewGuid().ToString("N")[..12]}");
                        payment.Succeed();
                        _context.Payments.Add(payment);
                    }
                }

                await _context.SaveChangesAsync();

                var allPayments = await _context.Payments
                    .Where(p => paidInvoices.Select(i => i.Id).Contains(p.InvoiceId))
                    .ToListAsync();

                foreach (var payment in allPayments)
                {
                    var matchInv = paidInvoices.FirstOrDefault(i => i.Id == payment.InvoiceId);
                    if (matchInv == null) continue;
                    var paidAt = matchInv.DueDate.AddDays(1);
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE payments SET paid_at = {0} WHERE id = {1}",
                        paidAt, payment.Id
                    );
                }

                var eventCount = await _context.SubscriptionEvents.CountAsync(e => e.SubscriptionId == subId);
                if (eventCount == 0)
                {
                    var events = new List<(string EventType, string Description, DateTime CreatedAt)>
                    {
                        ("Subscription Created", $"Plan {basicPlan.Name} activated. ${basicPlan.MonthlyPrice}/mo.", sixMonthsAgo),
                        ("Payment Received", "Payment received successfully.", sixMonthsAgo.AddDays(9)),
                        ("Payment Received", "Payment received successfully.", sixMonthsAgo.AddMonths(1).AddDays(9)),
                        ("Payment Received", "Payment received successfully.", sixMonthsAgo.AddMonths(2).AddDays(9)),
                        ("Plan Upgraded", $"Upgraded to {cfg.Plan.Name} plan. ${cfg.Plan.MonthlyPrice}/mo.", sixMonthsAgo.AddMonths(3).AddDays(1)),
                        ("Payment Received", "Payment received successfully.", sixMonthsAgo.AddMonths(3).AddDays(9)),
                        ("Payment Received", "Payment received successfully.", sixMonthsAgo.AddMonths(4).AddDays(9)),
                        ("Invoice Generated", "New invoice generated for next billing period.", oneMonthAgo.AddDays(1)),
                    };

                    foreach (var (eventType, description, createdAt) in events)
                    {
                        var evt = new SubscriptionEvent(subId, eventType, description);
                        _context.SubscriptionEvents.Add(evt);
                        await _context.SaveChangesAsync();

                        await _context.Database.ExecuteSqlRawAsync(
                            "UPDATE subscription_events SET created_at = {0} WHERE id = {1}",
                            createdAt, evt.Id
                        );
                    }
                }

                var existingCard = await _context.SavedCards.FirstOrDefaultAsync(c => c.LandlordId == landlord.Id);
                if (existingCard == null)
                {
                    var isJhosep = cfg.Email == "jh_slin@nexora.com";
                    var savedCard = new SavedCard(
                        landlord.Id,
                        isJhosep ? "Visa" : "MasterCard",
                        isJhosep ? "4111111111111111" : "5555555555554444",
                        isJhosep ? "12" : "08",
                        isJhosep ? "28" : "29",
                        isJhosep ? "Jhosep Argomedo" : "Dev Developer",
                        isJhosep ? "123" : "456",
                        true
                    );
                    _context.SavedCards.Add(savedCard);
                    await _context.SaveChangesAsync();

                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE saved_cards SET created_at = {0} WHERE id = {1}",
                        sixMonthsAgo, savedCard.Id
                    );
                }
            }
        }

        private async Task SeedNotificationPreferencesAsync()
        {
            var allUsers = await _context.Users.ToListAsync();
            foreach (var user in allUsers)
            {
                var existing = await _context.NotificationPreferences
                    .FirstOrDefaultAsync(n => n.UserId == user.Id);
                if (existing == null)
                {
                    var prefs = new NotificationPreference(user.Id, true, false);
                    _context.NotificationPreferences.Add(prefs);
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task SeedIoTDataAsync()
        {
            var properties = await _context.Properties.OrderBy(p => p.Id).ToListAsync();
            if (properties.Count == 0) return;

            // ── Devices (Integrados con campos reales de RSSI, MAC y Firmware) ──
            var deviceDefs = new[]
            {
                (Id: "water-safety-unit-apt-402", PropertyIdx: 0, ConnectionStatus: ConnectionStatus.Online, Mac: "00:1A:2B:3C:4D:60", Name: "water-safety-unit-apt-402", Rssi: -62, Firmware: "v2.4.1"),
                (Id: "voltage-safety-unit-apt-402", PropertyIdx: 0, ConnectionStatus: ConnectionStatus.Online, Mac: "00:1A:2B:3C:4D:5E", Name: "voltage-safety-unit-apt-402", Rssi: -68, Firmware: "v2.4.1"),
                (Id: "gas-safety-unit-apt-402", PropertyIdx: 0, ConnectionStatus: ConnectionStatus.Online, Mac: "00:1A:2B:3C:4D:5F", Name: "gas-safety-unit-apt-402", Rssi: -55, Firmware: "v2.4.1"),
                (Id: "safety-gateway-skyline-01", PropertyIdx: 4, ConnectionStatus: ConnectionStatus.Online, Mac: "00:1A:2B:3C:4D:61", Name: "safety-gateway-skyline-01", Rssi: -50, Firmware: "v2.4.1"),
                (Id: "safety-gateway-san-isidro-02", PropertyIdx: 3, ConnectionStatus: ConnectionStatus.Offline, Mac: "00:1A:2B:3C:4D:62", Name: "safety-gateway-san-isidro-02", Rssi: -85, Firmware: "v2.3.5"),
                (Id: "security-cam-condesa-01", PropertyIdx: 5, ConnectionStatus: ConnectionStatus.Online, Mac: "00:1A:2B:3C:4D:63", Name: "security-cam-condesa-01", Rssi: -60, Firmware: "v2.4.1"),
                (Id: "sensor-puerta-magdalena-01", PropertyIdx: 2, ConnectionStatus: ConnectionStatus.Online, Mac: "00:1A:2B:3C:4D:64", Name: "sensor-puerta-magdalena-01", Rssi: -70, Firmware: "v2.4.1"),
            };

            var now = DateTime.UtcNow;
            foreach (var def in deviceDefs)
            {
                if (def.PropertyIdx >= properties.Count) continue;
                var existing = await _context.Devices.FindAsync(def.Id);
                if (existing == null)
                {
                    existing = new Device(def.Id, def.ConnectionStatus, now.AddMinutes(-10), def.Mac, def.Name, def.Rssi, def.Firmware);
                    _context.Devices.Add(existing);
                }
                else
                {
                    existing.UpdateSync(def.ConnectionStatus, now.AddMinutes(-10));
                    existing.UpdateMacAddress(def.Mac);
                    existing.UpdateName(def.Name);
                    existing.UpdateRssi(def.Rssi);
                    existing.UpdateFirmwareVersion(def.Firmware);
                    _context.Devices.Update(existing);
                }
                existing.AssignToProperty(properties[def.PropertyIdx].Id);
            }
            await _context.SaveChangesAsync();

            // ── Telemetry (yearly, hourly) ──────────────────────────
            if (!await _context.TelemetryLogs.AnyAsync())
            {
                var waterDevice = await _context.Devices.FindAsync("water-safety-unit-apt-402");
                var powerDevice = await _context.Devices.FindAsync("voltage-safety-unit-apt-402");

                if (waterDevice != null && powerDevice != null)
                {
                    var rng = new Random(20260101);
                    var logs = new List<TelemetryLog>();

                    var oneYearAgo = now.AddDays(-365);
                    var fineGrainStart = now.AddDays(-45);
                    var timeline = new List<DateTime>();
                    for (var t = oneYearAgo; t < fineGrainStart; t = t.AddHours(4)) timeline.Add(t);
                    for (var t = fineGrainStart; t <= now; t = t.AddHours(1)) timeline.Add(t);

                    foreach (var ts in timeline)
                    {
                        double hour = ts.Hour + ts.Minute / 60.0;
                        double morning = Math.Exp(-Math.Pow(hour - 8.0, 2) / 6.0);
                        double evening = Math.Exp(-Math.Pow(hour - 20.0, 2) / 8.0);
                        double daily = morning + evening;
                        bool weekend = ts.DayOfWeek == DayOfWeek.Saturday || ts.DayOfWeek == DayOfWeek.Sunday;
                        double weekendBoost = weekend ? 1.25 : 1.0;
                        double seasonal = 1.0 + 0.15 * Math.Sin(2 * Math.PI * ts.DayOfYear / 365.0);

                        double waterFlow = (0.15 + 3.2 * daily * weekendBoost * seasonal) * (0.7 + rng.NextDouble() * 0.6);
                        if (rng.NextDouble() < 0.012) waterFlow += 18 + rng.NextDouble() * 10;
                        waterFlow = Math.Max(0, Math.Round(waterFlow, 2));

                        bool presence = daily > 0.25 ? rng.NextDouble() < 0.6 : rng.NextDouble() < 0.1;
                        logs.Add(new TelemetryLog(waterDevice.Id, waterFlow, 0, presence, 0, true, ts));

                        double current = (1.4 + 5.5 * daily * weekendBoost * seasonal) * (0.75 + rng.NextDouble() * 0.5);
                        if (rng.NextDouble() < 0.008) current += 14 + rng.NextDouble() * 9;
                        current = Math.Max(0, Math.Round(current, 2));
                        bool voltageOk = rng.NextDouble() > 0.01;
                        logs.Add(new TelemetryLog(powerDevice.Id, 0, 0, false, current, voltageOk, ts));
                    }

                    await _context.TelemetryLogs.AddRangeAsync(logs);
                    await _context.SaveChangesAsync();
                }

                // Recent telemetry for all devices (last 6 months, 5 samples each)
                var allDevices = await _context.Devices.ToListAsync();
                var deviceLogs = new List<TelemetryLog>();

                foreach (var device in allDevices)
                {
                    for (int m = 5; m >= 0; m--)
                    {
                        var monthDate = now.AddMonths(-m);
                        for (int l = 0; l < 5; l++)
                        {
                            var timestamp = new DateTime(monthDate.Year, monthDate.Month, Math.Min(monthDate.Day + l * 2 + 1, 28), 10 + l, 0, 0, DateTimeKind.Utc);
                            var rand = new Random();
                            deviceLogs.Add(new TelemetryLog(
                                device.Id,
                                1.0 + rand.NextDouble() * 3.0,
                                10.0 + rand.NextDouble() * 20.0,
                                m < 3 ? rand.NextDouble() > 0.3 : false,
                                8.0 + rand.NextDouble() * 12.0,
                                rand.NextDouble() > 0.05,
                                timestamp
                            ));
                        }
                    }
                }

                await _context.TelemetryLogs.AddRangeAsync(deviceLogs);
                await _context.SaveChangesAsync();

                // Anomalies
                var anomalyTime1 = now.AddHours(-2);
                var anomalyTime2 = now.AddHours(-1);
                var anomalyTime3 = now.AddMinutes(-30);

                _context.TelemetryLogs.Add(new TelemetryLog("voltage-safety-unit-apt-402", 2.1, 0.0, false, 15.4, false, anomalyTime1));
                _context.TelemetryLogs.Add(new TelemetryLog("voltage-safety-unit-apt-402", 1.8, 0.0, false, 22.5, true, anomalyTime2));
                _context.TelemetryLogs.Add(new TelemetryLog("gas-safety-unit-apt-402", 0.0, 320.0, false, 0.0, true, anomalyTime3));

                await _context.SaveChangesAsync();
            }

            // ── Alerts & Tickets ────────────────────────────────────
            if (!await _context.Alerts.AnyAsync())
            {
                var deviceIds = deviceDefs.Select(d => d.Id).ToArray();
                var alertTime1 = now.AddHours(-2);
                var alertTime2 = now.AddHours(-1);
                var alertTime3 = now.AddMinutes(-30);

                var alert1 = new Alert(AlertSeverity.Critical, "Voltage Instability Anomaly", alertTime1, "voltage-safety-unit-apt-402");
                _context.Alerts.Add(alert1);

                var alert2 = new Alert(AlertSeverity.Warning, "Overcurrent Detected", alertTime2, "voltage-safety-unit-apt-402");
                _context.Alerts.Add(alert2);

                var alert3 = new Alert(AlertSeverity.Critical, "Critical Gas Leak Level", alertTime3, "gas-safety-unit-apt-402");
                _context.Alerts.Add(alert3);

                await _context.SaveChangesAsync();

                var ticket1 = new MaintenanceTicket(alert1);
                ticket1.Assign("Ing. Carlos Mendoza");
                _context.MaintenanceTickets.Add(ticket1);

                var ticket2 = new MaintenanceTicket(alert2);
                _context.MaintenanceTickets.Add(ticket2);

                var ticket3 = new MaintenanceTicket(alert3);
                ticket3.Assign("Ing. Sofía Reyes");
                ticket3.Resolve();
                _context.MaintenanceTickets.Add(ticket3);

                await _context.SaveChangesAsync();
            }
        }
    }
}
