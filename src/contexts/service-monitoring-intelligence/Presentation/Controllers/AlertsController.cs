using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Nexora.Infrastructure.Persistence;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using System;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/alerts")]
    [Authorize]
    public class AlertsController : ControllerBase
    {
        private readonly NexoraDbContext _context;

        public AlertsController(NexoraDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAlerts(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? severity = null,
            [FromQuery] string? type = null,
            [FromQuery] bool? resolved = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !long.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var propertyIds = new List<long>();
            var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.UserId == userId);
            
            if (tenant != null)
            {
                if (tenant.PropertyId.HasValue)
                {
                    propertyIds.Add(tenant.PropertyId.Value);
                }
                else
                {
                    return Ok(new List<object>()); // Return empty list for unlinked tenants
                }
            }
            else
            {
                var landlord = await _context.Landlords.FirstOrDefaultAsync(l => l.UserId == userId);
                if (landlord != null)
                {
                    propertyIds = await _context.Properties
                        .Where(p => p.LandlordId == landlord.Id)
                        .Select(p => p.Id)
                        .ToListAsync();
                }
            }

            var deviceIds = await _context.Devices
                .Where(d => d.PropertyId != null && propertyIds.Contains(d.PropertyId.Value))
                .Select(d => d.Id)
                .ToListAsync();

            var query = _context.Alerts.Where(a => deviceIds.Contains(a.DeviceId));

            if (resolved.HasValue)
            {
                if (resolved.Value)
                {
                    query = query.Where(a => _context.MaintenanceTickets.Any(t => t.AlertId == a.Id && t.Status == Nexora.Domain.Enums.TicketStatus.Resolved));
                }
                else
                {
                    query = query.Where(a => !_context.MaintenanceTickets.Any(t => t.AlertId == a.Id && t.Status == Nexora.Domain.Enums.TicketStatus.Resolved));
                }
            }

            if (!string.IsNullOrEmpty(severity))
            {
                if (Enum.TryParse<AlertSeverity>(severity, true, out var severityEnum))
                {
                    query = query.Where(a => a.Severity == severityEnum);
                }
            }

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(a => a.Type.ToLower().Contains(type.ToLower()));
            }

            var total = await query.CountAsync();
            
            Response.Headers["X-Total-Count"] = total.ToString();
            Response.Headers["Access-Control-Expose-Headers"] = "X-Total-Count";

            var alerts = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new {
                    a.Id,
                    Severity = a.Severity.ToString(),
                    a.Timestamp,
                    a.DeviceId,
                    a.Type,
                    PropertyName = a.Device != null && a.Device.Property != null ? a.Device.Property.Name : "Unassigned",
                    Reading = _context.TelemetryLogs
                        .Where(t => t.DeviceId == a.DeviceId && t.Timestamp <= a.Timestamp)
                        .OrderByDescending(t => t.Timestamp)
                        .Select(t => a.Type.Contains("Gas") ? t.GasReading : 
                                     a.Type.Contains("Overcurrent") ? t.ElectricityReading : 
                                     a.Type.Contains("Voltage") ? (t.VoltageOk ? 1.0 : 0.0) :
                                     a.Type.Contains("Water") ? t.WaterReading : 0.0)
                        .FirstOrDefault(),
                    Status = _context.MaintenanceTickets.Any(t => t.AlertId == a.Id && t.Status == Nexora.Domain.Enums.TicketStatus.Resolved) ? "resolved" :
                             _context.MaintenanceTickets.Any(t => t.AlertId == a.Id) ? "pending" : "active"
                })
                .ToListAsync();

            return Ok(alerts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAlertById(long id)
        {
            var alert = await _context.Alerts
                .Include(a => a.Device)
                    .ThenInclude(d => d!.Property)
                .Where(a => a.Id == id)
                .FirstOrDefaultAsync();

            if (alert == null)
            {
                return NotFound("Alert not found");
            }

            var ticket = await _context.MaintenanceTickets
                .Where(t => t.AlertId == id)
                .Select(t => new {
                    t.Id,
                    Status = t.Status.ToString(),
                    t.AssignedTo,
                    t.CreatedAt,
                    t.ResolvedAt
                })
                .FirstOrDefaultAsync();

            // Also get the latest 10 telemetry readings for this device to show historical context (at or before the alert timestamp)
            var recentTelemetry = await _context.TelemetryLogs
                .Where(t => t.DeviceId == alert.DeviceId && t.Timestamp <= alert.Timestamp)
                .OrderByDescending(t => t.Timestamp)
                .Take(10)
                .Select(t => new {
                    t.WaterReading,
                    t.GasReading,
                    t.PresenceReading,
                    t.ElectricityReading,
                    t.VoltageOk,
                    t.Timestamp
                })
                .ToListAsync();

            return Ok(new {
                alert.Id,
                Severity = alert.Severity.ToString(),
                alert.Timestamp,
                alert.DeviceId,
                alert.Type,
                Device = alert.Device == null ? null : new {
                    alert.Device.Id,
                    ConnectionStatus = alert.Device.ConnectionStatus.ToString(),
                    alert.Device.LastSyncAt,
                    Property = alert.Device.Property == null ? null : new {
                        alert.Device.Property.Id,
                        alert.Device.Property.Name,
                        alert.Device.Property.Address,
                        alert.Device.Property.City,
                        alert.Device.Property.Country,
                        alert.Device.Property.IsSecurityModeArmed
                    }
                },
                Ticket = ticket,
                RecentTelemetry = recentTelemetry
            });
        }

        [HttpPost("{id}/tickets")]
        public async Task<IActionResult> CreateTicket(long id, [FromBody] CreateTicketRequestDto request)
        {
            var alert = await _context.Alerts.FindAsync(id);
            if (alert == null) return NotFound("Alert not found");

            var existingTicket = await _context.MaintenanceTickets.FirstOrDefaultAsync(t => t.AlertId == id);
            if (existingTicket != null) return BadRequest("Ticket already exists for this alert");

            var ticket = new MaintenanceTicket(id);
            if (request != null && !string.IsNullOrWhiteSpace(request.AssignedTo))
            {
                ticket.Assign(request.AssignedTo);
            }

            await _context.MaintenanceTickets.AddAsync(ticket);
            await _context.SaveChangesAsync();

            return StatusCode(201, new {
                ticket.Id,
                ticket.AlertId,
                Status = ticket.Status.ToString(),
                ticket.AssignedTo,
                ticket.CreatedAt,
                ticket.ResolvedAt
            });
        }

        [HttpPut("{id}/tickets/resolve")]
        public async Task<IActionResult> ResolveTicket(long id)
        {
            var alert = await _context.Alerts.FindAsync(id);
            if (alert == null) return NotFound("Alert not found");

            var ticket = await _context.MaintenanceTickets.FirstOrDefaultAsync(t => t.AlertId == id);
            if (ticket == null)
                return NotFound("No ticket exists for this alert. Create one first via POST alerts/{id}/tickets.");

            ticket.Resolve();
            await _context.SaveChangesAsync();

            return Ok(new {
                ticket.Id,
                ticket.AlertId,
                Status = ticket.Status.ToString(),
                ticket.AssignedTo,
                ticket.CreatedAt,
                ticket.ResolvedAt
            });
        }
    }

    public class CreateTicketRequestDto
    {
        public string? AssignedTo { get; set; }
    }
}
