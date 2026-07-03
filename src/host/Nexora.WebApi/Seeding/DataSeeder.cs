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
            if (!await _context.Users.AnyAsync())
            {
                var users = new[] {
                    new RegisterDto("test@example.com", "Nexora2026!", "Juan", "Pérez", "México", "CDMX", "Calle 123", "5512345678"),
                    new RegisterDto("jh_slin@nexora.com", "root", "Jhosep", "Argomedo", "México", "Ciudad de México", "96 Av. P.º de la Reforma", "978777386"),
                    new RegisterDto("sebasram@nexora.com", "Nexora2026!", "Sebastian", "Ramirez", "Argentina", "Resistencia", "Av. Chaco 743", "936083234"),
                    new RegisterDto("mario.pinedo@gmail.com", "Nexora2026!", "Mario", "Pinedo", "Perú", "Lima", "Av. La Molina 2550", "987654321")
                };

                foreach (var u in users)
                {
                    await _authService.RegisterAsync(u);
                }
            }

            // Ensure all users have a landlord profile (heals existing database states where landlords are missing)
            var dbUsers = await _context.Users.ToListAsync();
            foreach (var user in dbUsers)
            {
                var landlordExists = await _context.Landlords.AnyAsync(l => l.UserId == user.Id);
                if (!landlordExists)
                {
                    string firstName = "Admin";
                    string lastName = "User";
                    string country = "DefaultCountry";
                    string city = "DefaultCity";
                    string address = "DefaultAddress";
                    string? phone = null;

                    if (user.Email == "test@example.com") { firstName = "Juan"; lastName = "Pérez"; country = "México"; city = "CDMX"; address = "Calle 123"; phone = "5512345678"; }
                    else if (user.Email == "jh_slin@nexora.com") { firstName = "Jhosep"; lastName = "Argomedo"; country = "México"; city = "Ciudad de México"; address = "96 Av. P.º de la Reforma"; phone = "978777386"; }
                    else if (user.Email == "sebasram@nexora.com") { firstName = "Sebastian"; lastName = "Ramirez"; country = "Argentina"; city = "Resistencia"; address = "Av. Chaco 743"; phone = "936083234"; }
                    else if (user.Email == "mario.pinedo@gmail.com") { firstName = "Mario"; lastName = "Pinedo"; country = "Perú"; city = "Lima"; address = "Av. La Molina 2550"; phone = "987654321"; }

                    var landlord = new Landlord(user.Id, firstName, lastName, country, city, address, phone);
                    await _context.Landlords.AddAsync(landlord);
                }
            }
            await _context.SaveChangesAsync();

            if (!await _context.Properties.AnyAsync())
            {
                var properties = new[] {
                    (Name: "Departamento Barranco", Description: "Depa moderno con vista al mar", Type: PropertyType.APARTMENT, Country: "Peru", City: "Lima", Address: "Malecón Paul Harris 250", IsSecurityModeArmed: false, OwnerEmail: "jh_slin@nexora.com"),
                    (Name: "Local Comercial Centro de Lima", Description: "Local en zona de alto tránsito peatonal", Type: PropertyType.COMMERCIAL, Country: "Peru", City: "Lima", Address: "Jirón de la Unión 400", IsSecurityModeArmed: true, OwnerEmail: "jh_slin@nexora.com"),
                    (Name: "Oficina San Borja Tech", Description: "Oficina equipada para startup tecnológica", Type: PropertyType.OFFICE, Country: "Peru", City: "Lima", Address: "Av. San Borja Sur 600", IsSecurityModeArmed: false, OwnerEmail: "jh_slin@nexora.com"),
                    (Name: "Casa Magdalena", Description: "Casa familiar en Magdalena", Type: PropertyType.HOUSE, Country: "Perú", City: "Lima", Address: "Jr. Echenique 215, Dpto. 4", IsSecurityModeArmed: true, OwnerEmail: "jh_slin@nexora.com")
                };

                foreach (var p in properties)
                {
                    var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == p.OwnerEmail);
                    if (user == null) continue;

                    var cmd = new CreatePropertyCommand(p.Name, p.Description, p.Type, p.Country, p.City, p.Address, p.IsSecurityModeArmed, user.Id);
                    await _mediator.Send(cmd);
                }

                await _context.SaveChangesAsync();
            }

            await SeedTenantDataAsync();
            await SeedSubscriptionDataAsync();
            await SeedTelemetryDataAsync();
            await SeedDeveloperDataAsync();
        }

        /// <summary>
        /// Seeds a year of realistic water and electricity telemetry for two devices
        /// attached to the first property, so the Reports module shows real, varied
        /// data across every range (day / week / month / year). Idempotent: only runs
        /// when no telemetry exists yet.
        /// </summary>
        private async Task SeedTelemetryDataAsync()
        {
            if (await _context.TelemetryLogs.AnyAsync()) return;

            var property = await _context.Properties.OrderBy(p => p.Id).FirstOrDefaultAsync();
            if (property == null) return;

            const string waterDeviceId = "water-safety-unit-apt-402";
            const string powerDeviceId = "voltage-safety-unit-apt-402";

            var now = DateTime.UtcNow;

            // Ensure the two source devices exist and belong to the property.
            foreach (var id in new[] { waterDeviceId, powerDeviceId })
            {
                var device = await _context.Devices.FindAsync(id);
                if (device == null)
                {
                    device = new Device(id, ConnectionStatus.Online, now);
                    device.AssignToProperty(property.Id);
                    await _context.Devices.AddAsync(device);
                }
            }
            await _context.SaveChangesAsync();

            var rng = new Random(20260101);
            var logs = new List<TelemetryLog>();

            // Build the sampling timeline: hourly for the last 45 days (fine grain for
            // day/week/month views) and every 4 hours further back to one year (keeps the
            // year view populated without exploding the row count).
            var timeline = new List<DateTime>();
            var oneYearAgo = now.AddDays(-365);
            var fineGrainStart = now.AddDays(-45);
            for (var t = oneYearAgo; t < fineGrainStart; t = t.AddHours(4)) timeline.Add(t);
            for (var t = fineGrainStart; t <= now; t = t.AddHours(1)) timeline.Add(t);

            foreach (var ts in timeline)
            {
                // Diurnal shape: low overnight, peaks in the morning and the evening.
                double hour = ts.Hour + ts.Minute / 60.0;
                double morning = Math.Exp(-Math.Pow(hour - 8.0, 2) / 6.0);
                double evening = Math.Exp(-Math.Pow(hour - 20.0, 2) / 8.0);
                double daily = morning + evening;
                bool weekend = ts.DayOfWeek == DayOfWeek.Saturday || ts.DayOfWeek == DayOfWeek.Sunday;
                double weekendBoost = weekend ? 1.25 : 1.0;
                // Mild seasonal trend across the year.
                double seasonal = 1.0 + 0.15 * Math.Sin(2 * Math.PI * ts.DayOfYear / 365.0);

                // Water flow (L/min): mostly idle with morning/evening usage, rare spikes.
                double waterFlow = (0.15 + 3.2 * daily * weekendBoost * seasonal) * (0.7 + rng.NextDouble() * 0.6);
                if (rng.NextDouble() < 0.012) waterFlow += 18 + rng.NextDouble() * 10; // occasional leak/spike (> safe 20)
                waterFlow = Math.Max(0, Math.Round(waterFlow, 2));

                bool presence = daily > 0.25 ? rng.NextDouble() < 0.6 : rng.NextDouble() < 0.1;
                logs.Add(new TelemetryLog(waterDeviceId, waterFlow, 0, presence, 0, true, ts));

                // Electrical current (A): always-on baseline plus appliance peaks.
                double current = (1.4 + 5.5 * daily * weekendBoost * seasonal) * (0.75 + rng.NextDouble() * 0.5);
                if (rng.NextDouble() < 0.008) current += 14 + rng.NextDouble() * 9; // occasional overcurrent (> safe 20)
                current = Math.Max(0, Math.Round(current, 2));
                bool voltageOk = rng.NextDouble() > 0.01;
                logs.Add(new TelemetryLog(powerDeviceId, 0, 0, false, current, voltageOk, ts));
            }

            await _context.TelemetryLogs.AddRangeAsync(logs);
            await _context.SaveChangesAsync();
        }

        private async Task SeedTenantDataAsync()
        {
            if (await _context.Tenants.AnyAsync()) return;

            var properties = await _context.Properties.OrderBy(p => p.Id).ToListAsync();
            if (properties.Count < 4) return;

            var tenantsByProperty = new Dictionary<int, (string FirstName, string LastName, string Country, string City, string Address, string Phone)[]>
            {
                { 0, new[] { ("Carlos", "García", "Perú", "Lima", "Av. Larco 123", "999111001") } },
                { 1, new[] {
                    ("María", "López", "Perú", "Lima", "Jr. Unión 456", "999111002"),
                    ("José", "Martínez", "Perú", "Lima", "Av. Abancay 789", "999111003")
                }},
                { 2, new[] { ("Ana", "Rodríguez", "Perú", "Lima", "Calle Las Flores 321", "999111004") } },
                { 3, new[] {
                    ("Luis", "Fernández", "Perú", "Lima", "Av. Primavera 111", "999111005"),
                    ("Carmen", "Torres", "Perú", "Lima", "Jr. Los Olivos 222", "999111006"),
                    ("Pedro", "Sánchez", "Perú", "Lima", "Calle Sol 333", "999111007"),
                    ("Rosa", "Ramírez", "Perú", "Lima", "Av. Mariscal 444", "999111008")
                }}
            };

            foreach (var (index, tenants) in tenantsByProperty)
            {
                var property = properties[index];
                foreach (var t in tenants)
                {
                    var tenant = new Tenant(property.Id, t.FirstName, t.LastName, t.Country, t.City, t.Address, t.Phone);
                    _context.Tenants.Add(tenant);
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task SeedSubscriptionDataAsync()
        {
            var slinUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "jh_slin@nexora.com");
            if (slinUser == null) return;

            var slinLandlord = await _context.Landlords.FirstOrDefaultAsync(l => l.UserId == slinUser.Id);
            if (slinLandlord == null) return;

            var professionalPlan = await _context.SubscriptionPlans.FindAsync(2L);
            if (professionalPlan == null) return;

            var basicPlan = await _context.SubscriptionPlans.FindAsync(1L);
            if (basicPlan == null) return;

            var now = DateTime.UtcNow;
            var sixMonthsAgo = now.AddMonths(-6);

            // --- 1. Ensure subscription exists with temporally coherent dates ---
            // Timeline: user subscribed 6 months ago on Basic, upgraded to Professional at month 3
            // Current period: month 6 (started 1 month ago, ends 1 month from now)
            var subscription = await _context.Subscriptions
                .Include(s => s.Invoices)
                .FirstOrDefaultAsync(s => s.LandlordId == slinLandlord.Id);

            if (subscription == null)
            {
                subscription = new Subscription(
                    slinLandlord.Id,
                    basicPlan.Id,
                    sixMonthsAgo,
                    sixMonthsAgo.AddMonths(1)
                );

                _context.Subscriptions.Add(subscription);
                await _context.SaveChangesAsync();
            }

            // Force correct temporal dates via raw SQL update (bypasses private setters cleanly)
            var subId = subscription.Id;
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE subscriptions SET started_at = {0}, current_period_start = {1}, current_period_end = {2}, subscription_plan_id = {3} WHERE id = {4}",
                sixMonthsAgo,
                now.AddMonths(-1),
                now.AddMonths(1),
                professionalPlan.Id,
                subId
            );

            // --- 2. Invoices: one per month for 6 months ---
            // Months 1-3: Basic plan ($32.12), Months 4-6: Professional ($44.20)
            decimal[] monthlyAmounts = [
                basicPlan.MonthlyPrice,   // month 1
                basicPlan.MonthlyPrice,   // month 2
                basicPlan.MonthlyPrice,   // month 3
                professionalPlan.MonthlyPrice, // month 4 (upgrade)
                professionalPlan.MonthlyPrice, // month 5
                professionalPlan.MonthlyPrice  // month 6 (current)
            ];

            var existingInvoices = await _context.Invoices
                .Where(i => i.SubscriptionId == subId)
                .OrderBy(i => i.Id)
                .ToListAsync();

            if (existingInvoices.Count == 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    var periodStart = sixMonthsAgo.AddMonths(i);
                    var dueDate = periodStart.AddDays(7);
                    var inv = new Invoice(subId, monthlyAmounts[i], dueDate);
                    _context.Invoices.Add(inv);
                }

                await _context.SaveChangesAsync();

                existingInvoices = await _context.Invoices
                    .Where(i => i.SubscriptionId == subId)
                    .OrderBy(i => i.Id)
                    .ToListAsync();
            }

            // Set correct CreatedAt, status, amount, and due_date for each invoice via raw SQL
            for (int i = 0; i < existingInvoices.Count && i < 6; i++)
            {
                var inv = existingInvoices[i];
                var invCreatedAt = sixMonthsAgo.AddMonths(i).AddDays(1);
                var periodStart = sixMonthsAgo.AddMonths(i);
                var dueDate = periodStart.AddDays(7);

                var invStatus = i < 5 ? "Paid" : "Pending";

                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE invoices SET created_at = {0}, status = {1}, amount = {2}, due_date = {3} WHERE id = {4}",
                    invCreatedAt,
                    invStatus,
                    monthlyAmounts[i],
                    dueDate,
                    inv.Id
                );
            }

            // Remove any extra invoices beyond the expected 6
            if (existingInvoices.Count > 6)
            {
                var extraInvoiceIds = existingInvoices.Skip(6).Select(i => i.Id).ToList();
                // Delete associated payments first
                foreach (var extraId in extraInvoiceIds)
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM payments WHERE invoice_id = {0}", extraId);
                }
                // Delete events referencing deleted invoices (if any)
                // Then delete the invoices themselves
                foreach (var extraId in extraInvoiceIds)
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM invoices WHERE id = {0}", extraId);
                }
            }

            // --- 3. Payments: one per paid invoice (months 1-5) ---
            var paidInvoices = existingInvoices.Take(5).ToList();

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

            // Set correct paid_at dates via raw SQL
            var paidInvoiceIds = paidInvoices.Select(i => i.Id).ToList();

            var allPayments = await _context.Payments
                .Where(p => paidInvoiceIds.Contains(p.InvoiceId))
                .ToListAsync();

            foreach (var payment in allPayments)
            {
                var matchInv = paidInvoices.FirstOrDefault(i => i.Id == payment.InvoiceId);
                if (matchInv == null) continue;
                var paidAt = matchInv.DueDate.AddDays(1);
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE payments SET paid_at = {0} WHERE id = {1}",
                    paidAt,
                    payment.Id
                );
            }

            // --- 4. Subscription events reflecting real history ---
            var eventCount = await _context.SubscriptionEvents.CountAsync(e => e.SubscriptionId == subId);
            if (eventCount == 0)
            {
                var events = new List<(string EventType, string Description, DateTime CreatedAt)>
                {
                    ("Subscription Created", $"Plan {basicPlan.Name} activated. ${basicPlan.MonthlyPrice}/mo.", sixMonthsAgo),
                    ("Payment Received", "Payment received successfully.", sixMonthsAgo.AddDays(9)),
                    ("Payment Received", "Payment received successfully.", sixMonthsAgo.AddMonths(1).AddDays(9)),
                    ("Payment Received", "Payment received successfully.", sixMonthsAgo.AddMonths(2).AddDays(9)),
                    ("Plan Upgraded", $"Upgraded to {professionalPlan.Name} plan. ${professionalPlan.MonthlyPrice}/mo.", sixMonthsAgo.AddMonths(3).AddDays(1)),
                    ("Payment Received", "Payment received successfully.", sixMonthsAgo.AddMonths(3).AddDays(9)),
                    ("Payment Received", "Payment received successfully.", sixMonthsAgo.AddMonths(4).AddDays(9)),
                    ("Invoice Generated", "New invoice generated for next billing period.", now.AddMonths(-1).AddDays(1)),
                };

                foreach (var (eventType, description, createdAt) in events)
                {
                    var evt = new SubscriptionEvent(subId, eventType, description);
                    _context.SubscriptionEvents.Add(evt);
                    await _context.SaveChangesAsync();

                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE subscription_events SET created_at = {0} WHERE id = {1}",
                        createdAt,
                        evt.Id
                    );
                }
            }

            // --- 5. Saved card with full number, metadata, and coherent date ---
            var existingCard = await _context.SavedCards.FirstOrDefaultAsync(c => c.LandlordId == slinLandlord.Id);
            if (existingCard == null)
            {
                var savedCard = new SavedCard(
                    slinLandlord.Id,
                    "Visa",
                    "4111111111111111",
                    "12",
                    "28",
                    "Jhosep Argomedo",
                    "123",
                    true
                );
                _context.SavedCards.Add(savedCard);
                await _context.SaveChangesAsync();

                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE saved_cards SET created_at = {0} WHERE id = {1}",
                    sixMonthsAgo,
                    savedCard.Id
                );
            }
            else
            {
                // Always refresh card metadata - backfill FullNumber if needed, otherwise keep existing
                var fullNumber = "4111111111111111";
                var needsBackfill = false;
                try
                {
                    var prop = existingCard.GetType().GetProperty("FullNumber");
                    var val = prop?.GetValue(existingCard) as string;
                    needsBackfill = string.IsNullOrEmpty(val);
                }
                catch { needsBackfill = true; }

                if (needsBackfill)
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE saved_cards SET full_number = {0}, last_four = {1}, holder_name = {2}, expiry_month = {3}, expiry_year = {4}, is_default = {5}, created_at = {6} WHERE id = {7}",
                        fullNumber,
                        fullNumber.Length >= 4 ? fullNumber[^4..] : fullNumber,
                        "Jhosep Argomedo",
                        "12",
                        "28",
                        true,
                        sixMonthsAgo,
                        existingCard.Id
                    );
                }
                else
                {
                    // FullNumber already set - just refresh metadata
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE saved_cards SET holder_name = {0}, expiry_month = {1}, expiry_year = {2}, is_default = {3} WHERE id = {4}",
                        "Jhosep Argomedo",
                        "12",
                        "28",
                        true,
                        existingCard.Id
                    );
                }
            }
        }

        private async Task SeedDeveloperDataAsync()
        {
            var devUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "developer@nexora.com");
            if (devUser == null)
            {
                var registerDto = new RegisterDto("developer@nexora.com", "root", "Dev", "Developer", "Perú", "Lima", "Av. Javier Prado 100", "999999999");
                await _authService.RegisterAsync(registerDto);
                devUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "developer@nexora.com");
            }

            var devLandlord = await _context.Landlords.FirstOrDefaultAsync(l => l.UserId == devUser.Id);
            if (devLandlord == null) return;

            // --- 1. Subscription & Billing for developer@nexora.com ---
            var professionalPlan = await _context.SubscriptionPlans.FindAsync(2L);
            if (professionalPlan != null)
            {
                var now = DateTime.UtcNow;
                var sixMonthsAgo = now.AddMonths(-6);

                var devSubscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.LandlordId == devLandlord.Id);

                if (devSubscription == null)
                {
                    devSubscription = new Subscription(
                        devLandlord.Id,
                        professionalPlan.Id,
                        sixMonthsAgo,
                        sixMonthsAgo.AddMonths(1)
                    );
                    _context.Subscriptions.Add(devSubscription);
                    await _context.SaveChangesAsync();
                }

                var subId = devSubscription.Id;
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE subscriptions SET started_at = {0}, current_period_start = {1}, current_period_end = {2}, subscription_plan_id = {3} WHERE id = {4}",
                    sixMonthsAgo,
                    now.AddMonths(-1),
                    now.AddMonths(1),
                    professionalPlan.Id,
                    subId
                );

                // 6 Invoices
                decimal[] monthlyAmounts = [
                    professionalPlan.MonthlyPrice,
                    professionalPlan.MonthlyPrice,
                    professionalPlan.MonthlyPrice,
                    professionalPlan.MonthlyPrice,
                    professionalPlan.MonthlyPrice,
                    professionalPlan.MonthlyPrice
                ];

                var devInvoices = await _context.Invoices
                    .Where(i => i.SubscriptionId == subId)
                    .OrderBy(i => i.Id)
                    .ToListAsync();

                if (devInvoices.Count == 0)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        var periodStart = sixMonthsAgo.AddMonths(i);
                        var dueDate = periodStart.AddDays(7);
                        var inv = new Invoice(subId, monthlyAmounts[i], dueDate);
                        _context.Invoices.Add(inv);
                    }
                    await _context.SaveChangesAsync();

                    devInvoices = await _context.Invoices
                        .Where(i => i.SubscriptionId == subId)
                        .OrderBy(i => i.Id)
                        .ToListAsync();
                }

                for (int i = 0; i < devInvoices.Count && i < 6; i++)
                {
                    var inv = devInvoices[i];
                    var invCreatedAt = sixMonthsAgo.AddMonths(i).AddDays(1);
                    var periodStart = sixMonthsAgo.AddMonths(i);
                    var dueDate = periodStart.AddDays(7);
                    var invStatus = i < 5 ? "Paid" : "Pending";

                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE invoices SET created_at = {0}, status = {1}, amount = {2}, due_date = {3} WHERE id = {4}",
                        invCreatedAt,
                        invStatus,
                        monthlyAmounts[i],
                        dueDate,
                        inv.Id
                    );
                }

                // Payments for paid invoices
                var paidDevInvoices = devInvoices.Take(5).ToList();
                foreach (var inv in paidDevInvoices)
                {
                    var hasPayment = await _context.Payments.AnyAsync(p => p.InvoiceId == inv.Id);
                    if (!hasPayment)
                    {
                        var payment = new Payment(inv.Id, inv.Amount, "stripe", $"pi_dev_{Guid.NewGuid().ToString("N")[..12]}");
                        payment.Succeed();
                        _context.Payments.Add(payment);
                    }
                }
                await _context.SaveChangesAsync();

                var paidDevInvoiceIds = paidDevInvoices.Select(i => i.Id).ToList();
                var devPayments = await _context.Payments
                    .Where(p => paidDevInvoiceIds.Contains(p.InvoiceId))
                    .ToListAsync();

                foreach (var payment in devPayments)
                {
                    var matchInv = paidDevInvoices.FirstOrDefault(i => i.Id == payment.InvoiceId);
                    if (matchInv == null) continue;
                    var paidAt = matchInv.DueDate.AddDays(1);
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE payments SET paid_at = {0} WHERE id = {1}",
                        paidAt,
                        payment.Id
                    );
                }

                // Saved Card
                var devCard = await _context.SavedCards.FirstOrDefaultAsync(c => c.LandlordId == devLandlord.Id);
                if (devCard == null)
                {
                    devCard = new SavedCard(
                        devLandlord.Id,
                        "MasterCard",
                        "5555555555555555",
                        "09",
                        "29",
                        "Dev Developer",
                        "987",
                        true
                    );
                    _context.SavedCards.Add(devCard);
                    await _context.SaveChangesAsync();

                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE saved_cards SET created_at = {0} WHERE id = {1}",
                        sixMonthsAgo,
                        devCard.Id
                    );
                }
            }

            // --- 2. Properties ---
            var devProperties = await _context.Properties
                .Where(p => p.LandlordId == devLandlord.Id)
                .ToListAsync();

            if (devProperties.Count == 0)
            {
                var properties = new[] {
                    (Name: "Barranco Ocean View Suite", Description: "Ocean view apartment", Type: PropertyType.APARTMENT, Country: "Peru", City: "Lima", Address: "Malecón Paul Harris 250", IsSecurityModeArmed: false),
                    (Name: "Skyline Industrial Sector A", Description: "Industrial logistics warehouse", Type: PropertyType.COMMERCIAL, Country: "Peru", City: "Lima", Address: "Av. Industrial 400", IsSecurityModeArmed: true),
                    (Name: "San Isidro Offices", Description: "Corporate office building", Type: PropertyType.OFFICE, Country: "Peru", City: "Lima", Address: "Av. San Borja Sur 600", IsSecurityModeArmed: false)
                };

                foreach (var p in properties)
                {
                    var cmd = new CreatePropertyCommand(p.Name, p.Description, p.Type, p.Country, p.City, p.Address, p.IsSecurityModeArmed, devUser.Id);
                    await _mediator.Send(cmd);
                }
                await _context.SaveChangesAsync();

                devProperties = await _context.Properties
                    .Where(p => p.LandlordId == devLandlord.Id)
                    .ToListAsync();
            }

            // --- 3. Tenants ---
            var hasDevTenants = await _context.Tenants.AnyAsync(t => devProperties.Select(p => p.Id).Contains(t.PropertyId));
            if (!hasDevTenants && devProperties.Count >= 3)
            {
                var tenants = new[] {
                    new Tenant(devProperties[0].Id, "Juan", "Pérez", "Perú", "Lima", "Av. Larco 123", "999111001"),
                    new Tenant(devProperties[1].Id, "Sofía", "Martínez", "Perú", "Lima", "Jr. Unión 456", "999111002"),
                    new Tenant(devProperties[2].Id, "Carlos", "Fernández", "Perú", "Lima", "Av. Primavera 111", "999111005")
                };

                foreach (var tenant in tenants)
                {
                    _context.Tenants.Add(tenant);
                }
                await _context.SaveChangesAsync();
            }

            // --- 4. Devices ---
            if (devProperties.Count >= 3)
            {
                var propBarranco = devProperties[0];
                var propSkyline = devProperties[1];
                var propSanIsidro = devProperties[2];

                var devicesToSeed = new[] {
                    (Id: "voltage-safety-unit-apt-402", ConnectionStatus: ConnectionStatus.Online, PropertyId: propBarranco.Id),
                    (Id: "gas-safety-unit-apt-402", ConnectionStatus: ConnectionStatus.Online, PropertyId: propBarranco.Id),
                    (Id: "safety-gateway-skyline-01", ConnectionStatus: ConnectionStatus.Online, PropertyId: propSkyline.Id),
                    (Id: "safety-gateway-san-isidro-02", ConnectionStatus: ConnectionStatus.Offline, PropertyId: propSanIsidro.Id)
                };

                foreach (var devData in devicesToSeed)
                {
                    var existingDev = await _context.Devices.FindAsync(devData.Id);
                    if (existingDev == null)
                    {
                        existingDev = new Device(devData.Id, devData.ConnectionStatus, DateTime.UtcNow.AddMinutes(-10));
                        _context.Devices.Add(existingDev);
                    }
                    existingDev.AssignToProperty(devData.PropertyId);
                }
                await _context.SaveChangesAsync();
            }

            // --- 5. Telemetry Logs ---
            var deviceIds = new[] { "voltage-safety-unit-apt-402", "gas-safety-unit-apt-402", "safety-gateway-skyline-01", "safety-gateway-san-isidro-02" };
            var nowTime = DateTime.UtcNow;
            
            var existingLogs = await _context.TelemetryLogs
                .Where(t => deviceIds.Contains(t.DeviceId))
                .ToListAsync();
            if (existingLogs.Any())
            {
                _context.TelemetryLogs.RemoveRange(existingLogs);
                await _context.SaveChangesAsync();
            }

            {
                var rand = new Random();
                for (int m = 5; m >= 0; m--)
                {
                    var monthDate = nowTime.AddMonths(-m);
                    for (int l = 0; l < 5; l++)
                    {
                        var timestamp = new DateTime(monthDate.Year, monthDate.Month, Math.Min(monthDate.Day + l * 2 + 1, 28), 10 + l, 0, 0, DateTimeKind.Utc);
                        
                        _context.TelemetryLogs.Add(new TelemetryLog(
                            "voltage-safety-unit-apt-402",
                            1.0 + rand.NextDouble() * 3.0,
                            0.0,
                            false,
                            12.0 + rand.NextDouble() * 5.0,
                            true,
                            timestamp
                        ));

                        _context.TelemetryLogs.Add(new TelemetryLog(
                            "gas-safety-unit-apt-402",
                            0.0,
                            30.0 + rand.NextDouble() * 40.0,
                            false,
                            0.0,
                            true,
                            timestamp
                        ));

                        _context.TelemetryLogs.Add(new TelemetryLog(
                            "safety-gateway-skyline-01",
                            0.0,
                            0.0,
                            true,
                            8.0 + rand.NextDouble() * 6.0,
                            true,
                            timestamp
                        ));

                        _context.TelemetryLogs.Add(new TelemetryLog(
                            "safety-gateway-san-isidro-02",
                            2.0 + rand.NextDouble() * 2.0,
                            0.0,
                            false,
                            5.0 + rand.NextDouble() * 4.0,
                            true,
                            timestamp
                        ));
                    }
                }

                var anomalyTime1 = nowTime.AddHours(-2);
                var anomalyTime2 = nowTime.AddHours(-1);
                var anomalyTime3 = nowTime.AddMinutes(-30);

                _context.TelemetryLogs.Add(new TelemetryLog(
                    "voltage-safety-unit-apt-402",
                    2.1,
                    0.0,
                    false,
                    15.4,
                    false, // Anomaly!
                    anomalyTime1
                ));

                _context.TelemetryLogs.Add(new TelemetryLog(
                    "voltage-safety-unit-apt-402",
                    1.8,
                    0.0,
                    false,
                    22.5, // Anomaly!
                    true,
                    anomalyTime2
                ));

                _context.TelemetryLogs.Add(new TelemetryLog(
                    "gas-safety-unit-apt-402",
                    0.0,
                    320.0, // Anomaly!
                    false,
                    0.0,
                    true,
                    anomalyTime3
                ));

                await _context.SaveChangesAsync();
            }

            // --- 6. Alerts & Tickets ---
            var hasAlerts = await _context.Alerts.AnyAsync(a => deviceIds.Contains(a.DeviceId));
            if (!hasAlerts)
            {
                var alertTime1 = nowTime.AddHours(-2);
                var alertTime2 = nowTime.AddHours(-1);
                var alertTime3 = nowTime.AddMinutes(-30);

                var alert1 = new Alert(
                    AlertSeverity.Critical,
                    "Voltage Instability Anomaly",
                    alertTime1,
                    "voltage-safety-unit-apt-402"
                );
                _context.Alerts.Add(alert1);

                var alert2 = new Alert(
                    AlertSeverity.Warning,
                    "Overcurrent Detected",
                    alertTime2,
                    "voltage-safety-unit-apt-402"
                );
                _context.Alerts.Add(alert2);

                var alert3 = new Alert(
                    AlertSeverity.Critical,
                    "Critical Gas Leak Level",
                    alertTime3,
                    "gas-safety-unit-apt-402"
                );
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
