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
            // Keep the subscription plans aligned with the current product spec
            // on every startup (idempotent UPDATEs), even for an already-seeded DB.
            await SeedOrUpdatePlansAsync();

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
        }

        /// <summary>
        /// Keeps the two subscription plans aligned with the current product spec:
        /// Basic ($0.99/mo, up to 2 properties) and Plus ($5/mo, unlimited). Idempotent.
        /// </summary>
        private async Task SeedOrUpdatePlansAsync()
        {
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE subscription_plans SET name = 'Basic', monthly_price = 0.99, max_properties_limit = 2, unlimited_properties = FALSE WHERE id = 1");
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE subscription_plans SET name = 'Plus', monthly_price = 5.00, max_properties_limit = 0, unlimited_properties = TRUE WHERE id = 2");
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
    }
}
