using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nexora.Application.Dto;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Infrastructure.Persistence;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Domain.Resources;
using Swashbuckle.AspNetCore.Annotations;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/alerts")]
    [Authorize]
    [SwaggerTag("Alerts & Maintenance")]
    public class AlertsController : ControllerBase
    {
        private readonly NexoraDbContext _context;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public AlertsController(NexoraDbContext context, IStringLocalizer<SharedMessages> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        /// <summary>
        /// Returns paginated alerts for devices the user has access to.
        /// Supports filtering by severity, type, resolution status, and date range.
        /// </summary>
        [HttpGet]
        [SwaggerOperation(Summary = "List alerts", Description = "Returns paginated alerts with optional filtering by severity, type, resolution status, and date range.")]
        [ProducesResponseType(typeof(List<AlertListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAlerts(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? severity = null,
            [FromQuery] string? type = null,
            [FromQuery] bool? resolved = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? format = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var propertyIds = new List<long>();
            var landlord = await _context.Landlords.FirstOrDefaultAsync(l => l.UserId == userId);
            
            if (landlord != null)
            {
                propertyIds = await _context.Properties
                    .Where(p => p.LandlordId == landlord.Id)
                    .Select(p => p.Id)
                    .ToListAsync();
            }
            else
            {
                var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.UserId == userId);
                if (tenant != null && tenant.PropertyId.HasValue)
                {
                    propertyIds.Add(tenant.PropertyId.Value);
                }
                else
                {
                    return Ok(new List<AlertListItemDto>());
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
                    query = query.Where(a => _context.MaintenanceTickets.Any(t => t.AlertId == a.Id && t.Status == TicketStatus.Resolved));
                }
                else
                {
                    query = query.Where(a => !_context.MaintenanceTickets.Any(t => t.AlertId == a.Id && t.Status == TicketStatus.Resolved));
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

            if (startDate.HasValue)
            {
                var utcStart = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
                query = query.Where(a => a.Timestamp >= utcStart);
            }

            if (endDate.HasValue)
            {
                var utcEnd = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);
                query = query.Where(a => a.Timestamp <= utcEnd);
            }

            var total = await query.CountAsync();
            
            Response.Headers["X-Total-Count"] = total.ToString();
            Response.Headers["Access-Control-Expose-Headers"] = "X-Total-Count";

            var alerts = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AlertListItemDto(
                    a.Id,
                    a.Severity.ToString(),
                    a.Timestamp,
                    a.DeviceId,
                    a.Type,
                    a.Device != null && a.Device.Property != null ? a.Device.Property.Name : "Unassigned",
                    _context.TelemetryLogs
                        .Where(t => t.DeviceId == a.DeviceId && t.Timestamp <= a.Timestamp)
                        .OrderByDescending(t => t.Timestamp)
                        .Select(t => a.Type.Contains("Gas") ? t.GasReading : 
                                     a.Type.Contains("Overcurrent") ? t.ElectricityReading : 
                                     a.Type.Contains("Voltage") ? (t.VoltageOk ? 1.0 : 0.0) :
                                     a.Type.Contains("Water") ? t.WaterReading : 0.0)
                        .FirstOrDefault(),
                    _context.MaintenanceTickets.Any(t => t.AlertId == a.Id && t.Status == TicketStatus.Resolved) ? "resolved" :
                         _context.MaintenanceTickets.Any(t => t.AlertId == a.Id) ? "pending" : "active"
                ))
                .ToListAsync();

            return Ok(alerts);
        }

        /// <summary>
        /// Returns detailed information about a specific alert.
        /// </summary>
        [HttpGet("{alertId}")]
        [SwaggerOperation(Summary = "Get alert details", Description = "Returns full alert details with device info, property, ticket status, and recent telemetry history.")]
        [ProducesResponseType(typeof(AlertDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAlertById(long alertId)
        {
            var alert = await _context.Alerts
                .Include(a => a.Device)
                    .ThenInclude(d => d!.Property)
                .Where(a => a.Id == alertId)
                .FirstOrDefaultAsync();

            if (alert == null)
            {
                return NotFound(new ErrorResponse("NotFound", _localizer["Alert_NotFound"]));
            }

            var ticket = await _context.MaintenanceTickets
                .Where(t => t.AlertId == alertId)
                .Select(t => new AlertTicketDto(
                    t.Id,
                    t.Status.ToString(),
                    t.AssignedTo,
                    t.CreatedAt,
                    t.ResolvedAt
                ))
                .FirstOrDefaultAsync();

            var recentTelemetry = await _context.TelemetryLogs
                .Where(t => t.DeviceId == alert.DeviceId && t.Timestamp <= alert.Timestamp)
                .OrderByDescending(t => t.Timestamp)
                .Take(10)
                .Select(t => new AlertTelemetryEntryDto(
                    t.WaterReading,
                    t.GasReading,
                    t.PresenceReading,
                    t.ElectricityReading,
                    t.VoltageOk,
                    t.Timestamp
                ))
                .ToListAsync();

            AlertDeviceDto? deviceDto = null;
            if (alert.Device != null)
            {
                AlertPropertyDto? propertyDto = null;
                if (alert.Device.Property != null)
                {
                    propertyDto = new AlertPropertyDto(
                        alert.Device.Property.Id,
                        alert.Device.Property.Name,
                        alert.Device.Property.Address,
                        alert.Device.Property.City,
                        alert.Device.Property.Country,
                        alert.Device.Property.IsSecurityModeArmed
                    );
                }
                deviceDto = new AlertDeviceDto(
                    alert.Device.Id,
                    alert.Device.ConnectionStatus.ToString(),
                    alert.Device.LastSyncAt,
                    propertyDto
                );
            }

            return Ok(new AlertDetailDto(
                alert.Id,
                alert.Severity.ToString(),
                alert.Timestamp,
                alert.DeviceId,
                alert.Type,
                deviceDto,
                ticket,
                recentTelemetry
            ));
        }

    }

    [ApiController]
    [Route("api/v1/maintenance-tickets")]
    [Authorize]
    [SwaggerTag("Maintenance Tickets")]
    public class MaintenanceTicketsController : ControllerBase
    {
        private readonly NexoraDbContext _context;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public MaintenanceTicketsController(NexoraDbContext context, IStringLocalizer<SharedMessages> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        /// <summary>
        /// Returns maintenance tickets. Filter by alertId, status, or assignedTo.
        /// </summary>
        [HttpGet]
        [SwaggerOperation(Summary = "List maintenance tickets", Description = "Returns maintenance tickets with optional filtering by alert, status, or assignee.")]
        [ProducesResponseType(typeof(List<MaintenanceTicketDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetTickets(
            [FromQuery] long? alertId = null,
            [FromQuery] string? status = null,
            [FromQuery] string? assignedTo = null)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var query = _context.MaintenanceTickets.AsQueryable();

            if (alertId.HasValue)
                query = query.Where(t => t.AlertId == alertId.Value);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<TicketStatus>(status, true, out var statusEnum))
                query = query.Where(t => t.Status == statusEnum);

            if (!string.IsNullOrEmpty(assignedTo))
                query = query.Where(t => t.AssignedTo != null && t.AssignedTo.ToLower().Contains(assignedTo.ToLower()));

            var tickets = await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new MaintenanceTicketDto(
                    t.Id,
                    t.AlertId,
                    t.Status.ToString(),
                    t.AssignedTo,
                    t.CreatedAt,
                    t.ResolvedAt
                ))
                .ToListAsync();

            return Ok(tickets);
        }

        /// <summary>
        /// Creates a maintenance ticket for an alert.
        /// </summary>
        [HttpPost]
        [SwaggerOperation(Summary = "Create maintenance ticket", Description = "Creates a ticket for an alert to track its resolution.")]
        [ProducesResponseType(typeof(MaintenanceTicketDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequestDto request)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var alert = await _context.Alerts.FindAsync(request.AlertId);
            if (alert == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Alert_NotFound"]));

            var existingTicket = await _context.MaintenanceTickets.FirstOrDefaultAsync(t => t.AlertId == request.AlertId);
            if (existingTicket != null)
                return BadRequest(new ErrorResponse("BadRequest", _localizer["Ticket_AlreadyExists"]));

            var ticket = new MaintenanceTicket(request.AlertId);
            if (!string.IsNullOrWhiteSpace(request.AssignedTo))
            {
                ticket.Assign(request.AssignedTo);
            }

            await _context.MaintenanceTickets.AddAsync(ticket);
            await _context.SaveChangesAsync();

            return StatusCode(201, new MaintenanceTicketDto(
                ticket.Id,
                ticket.AlertId,
                ticket.Status.ToString(),
                ticket.AssignedTo,
                ticket.CreatedAt,
                ticket.ResolvedAt
            ));
        }

        /// <summary>
        /// Returns a specific maintenance ticket by ID.
        /// </summary>
        [HttpGet("{ticketId:long}")]
        [SwaggerOperation(Summary = "Get ticket by ID", Description = "Returns details for a specific maintenance ticket.")]
        [ProducesResponseType(typeof(MaintenanceTicketDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetTicketById(long ticketId)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var ticket = await _context.MaintenanceTickets
                .Where(t => t.Id == ticketId)
                .Select(t => new MaintenanceTicketDto(
                    t.Id,
                    t.AlertId,
                    t.Status.ToString(),
                    t.AssignedTo,
                    t.CreatedAt,
                    t.ResolvedAt
                ))
                .FirstOrDefaultAsync();

            if (ticket == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Ticket_NotFound"]));

            return Ok(ticket);
        }

        /// <summary>
        /// Updates a maintenance ticket (e.g., resolve it or reassign it).
        /// </summary>
        [HttpPatch("{ticketId:long}")]
        [SwaggerOperation(Summary = "Update maintenance ticket", Description = "Updates a ticket's status (e.g., resolve) or assignment.")]
        [ProducesResponseType(typeof(MaintenanceTicketDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateTicket(long ticketId, [FromBody] UpdateTicketRequestDto? request = null)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var ticket = await _context.MaintenanceTickets
                .FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Ticket_NotFound"]));

            if (request != null)
            {
                if (request.Resolved == true)
                {
                    ticket.Resolve();
                }
                if (!string.IsNullOrWhiteSpace(request.AssignedTo))
                {
                    ticket.Assign(request.AssignedTo);
                }
            }
            else
            {
                ticket.Resolve();
            }

            await _context.SaveChangesAsync();

            return Ok(new MaintenanceTicketDto(
                ticket.Id,
                ticket.AlertId,
                ticket.Status.ToString(),
                ticket.AssignedTo,
                ticket.CreatedAt,
                ticket.ResolvedAt
            ));
        }
    }

    public class CreateTicketRequestDto
    {
        public long AlertId { get; set; }
        public string? AssignedTo { get; set; }
    }

    public class UpdateTicketRequestDto
    {
        public bool? Resolved { get; set; }
        public string? AssignedTo { get; set; }
    }
}
