using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nexora.Application.Commands.Property;
using Nexora.Application.Dto;
using Nexora.Domain.Enums;
using Nexora.Infrastructure.Persistence;
using Nexora.Shared.Domain.Api;
using Nexora.WebApi.DTOs;
using Nexora.Shared.Domain.Resources;
using Swashbuckle.AspNetCore.Annotations;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/properties")]
    [Authorize]
    [SwaggerTag("Property Management")]
    public class PropertiesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly NexoraDbContext _context;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public PropertiesController(IMediator mediator, NexoraDbContext context, IStringLocalizer<SharedMessages> localizer)
        {
            _mediator = mediator;
            _context = context;
            _localizer = localizer;
        }

        [HttpPost]
        [SwaggerOperation(Summary = "Create a property", Description = "Creates a new property record for the authenticated landlord.")]
        [ProducesResponseType(typeof(long), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] CreatePropertyRequest request)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            try
            {
                var command = new CreatePropertyCommand(
                    request.Name,
                    request.Description,
                    request.Type,
                    request.Country,
                    request.City,
                    request.Address,
                    request.IsSecurityModeArmed,
                    userId.Value
                );
                var id = await _mediator.Send(command);
                return CreatedAtAction(nameof(GetById), new { propertyId = id }, id);
            }
            catch (Exception ex)
            {
                return BadRequest(new ErrorResponse("BadRequest", ex.Message));
            }
        }

        [HttpGet]
        [SwaggerOperation(Summary = "List properties", Description = "Returns all properties accessible to the user. Use ?code= to filter by property code.")]
        [ProducesResponseType(typeof(List<PropertyDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(PropertyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAll([FromQuery] string? code = null)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            if (!string.IsNullOrEmpty(code))
            {
                var property = await _context.Properties
                    .Include(p => p.Landlord)
                    .FirstOrDefaultAsync(p => p.PropertyCode == code && (p.Landlord.UserId == userId.Value || p.Tenants.Any(t => t.UserId == userId.Value)));

                if (property == null)
                    return NotFound(new ErrorResponse("NotFound", _localizer["Property_NotFound"]));

                var healthScore = await CalculateHealthScoreAsync(property.Id);

                return Ok(new PropertyDto(
                    property.Id,
                    property.PropertyCode,
                    property.Name,
                    property.Description,
                    property.PropertyType,
                    property.Country,
                    property.City,
                    property.Address,
                    property.Status,
                    property.IsSecurityModeArmed,
                    property.CreatedAt,
                    property.UpdatedAt,
                    new LandlordDto(
                        property.Landlord.Id,
                        property.Landlord.UserId,
                        property.Landlord.FirstName,
                        property.Landlord.LastName,
                        property.Landlord.PhoneNumber
                    ),
                    healthScore
                ));
            }

            var properties = await _context.Properties
                .Include(p => p.Landlord)
                .Include(p => p.Tenants)
                .Where(p => p.Landlord.UserId == userId.Value || p.Tenants.Any(t => t.UserId == userId.Value))
                .ToListAsync();

            var dtos = new List<PropertyDto>();
            foreach (var p in properties)
            {
                var healthScore = await CalculateHealthScoreAsync(p.Id);
                dtos.Add(new PropertyDto(
                    p.Id,
                    p.PropertyCode,
                    p.Name,
                    p.Description,
                    p.PropertyType,
                    p.Country,
                    p.City,
                    p.Address,
                    p.Status,
                    p.IsSecurityModeArmed,
                    p.CreatedAt,
                    p.UpdatedAt,
                    new LandlordDto(
                        p.Landlord.Id,
                        p.Landlord.UserId,
                        p.Landlord.FirstName,
                        p.Landlord.LastName,
                        p.Landlord.PhoneNumber
                    ),
                    healthScore
                ));
            }

            return Ok(dtos);
        }

        [HttpGet("{propertyId}")]
        [SwaggerOperation(Summary = "Get property by ID", Description = "Returns detailed information for a specific property including health score.")]
        [ProducesResponseType(typeof(PropertyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long propertyId)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var property = await _context.Properties
                .Include(p => p.Landlord)
                .FirstOrDefaultAsync(p => p.Id == propertyId && (p.Landlord.UserId == userId.Value || p.Tenants.Any(t => t.UserId == userId.Value)));

            if (property == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Property_NotFound"]));

            var healthScore = await CalculateHealthScoreAsync(property.Id);

            return Ok(new PropertyDto(
                property.Id,
                property.PropertyCode,
                property.Name,
                property.Description,
                property.PropertyType,
                property.Country,
                property.City,
                property.Address,
                property.Status,
                property.IsSecurityModeArmed,
                property.CreatedAt,
                property.UpdatedAt,
                new LandlordDto(
                    property.Landlord.Id,
                    property.Landlord.UserId,
                    property.Landlord.FirstName,
                    property.Landlord.LastName,
                    property.Landlord.PhoneNumber
                ),
                healthScore
            ));
        }

        [HttpGet("~/api/v1/property-statistics")]
        [SwaggerOperation(Summary = "Get property statistics", Description = "Returns aggregated property statistics for the current user.")]
        [ProducesResponseType(typeof(PropertySummaryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetStatistics()
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var total = await _context.Properties
                .CountAsync(p => p.Landlord.UserId == userId.Value || p.Tenants.Any(t => t.UserId == userId.Value));

            var protectedCount = await _context.Properties.CountAsync(p =>
                (p.Landlord.UserId == userId.Value || p.Tenants.Any(t => t.UserId == userId.Value)) &&
                p.Status == PropertyStatus.ACTIVE &&
                p.IsSecurityModeArmed);

            return Ok(new PropertySummaryDto(total, protectedCount));
        }

        [HttpPatch("{propertyId}")]
        [SwaggerOperation(Summary = "Partially update a property", Description = "Updates specific fields of a property (e.g., status, name, security mode).")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PartialUpdate(long propertyId, [FromBody] UpdatePropertyRequest request)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var owned = await _context.Properties
                .AnyAsync(p => p.Id == propertyId && p.Landlord.UserId == userId.Value);
            if (!owned)
                return NotFound(new ErrorResponse("NotFound", _localizer["Property_NotOwnedOrNotFound"]));

            var command = new UpdatePropertyCommand(
                propertyId,
                request.Name,
                request.Description,
                request.Type,
                request.Country,
                request.City,
                request.Address,
                request.Status,
                request.IsSecurityModeArmed
            );
            var result = await _mediator.Send(command);
            if (!result)
                return NotFound(new ErrorResponse("NotFound", _localizer["Property_NotFound"]));
            return NoContent();
        }

        [HttpDelete("{propertyId}")]
        [SwaggerOperation(Summary = "Delete a property", Description = "Removes a property and its associated data.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long propertyId)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var property = await _context.Properties
                .FirstOrDefaultAsync(p => p.Id == propertyId && p.Landlord.UserId == userId.Value);

            if (property == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Property_NotOwnedOrNotFound"]));

            _context.Properties.Remove(property);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<int?> CalculateHealthScoreAsync(long propertyId)
        {
            var devices = await _context.Devices
                .Where(d => d.PropertyId == propertyId)
                .ToListAsync();

            if (!devices.Any()) return null;

            var offlineCount = devices.Count(d => d.ConnectionStatus == ConnectionStatus.Offline);

            var deviceIds = devices.Select(d => d.Id).ToList();
            var criticalAlertCount = await _context.Alerts
                .Where(a => deviceIds.Contains(a.DeviceId) &&
                            a.Severity == AlertSeverity.Critical &&
                            a.Timestamp >= DateTime.UtcNow.AddDays(-1))
                .Select(a => new { a.DeviceId, a.Type })
                .Distinct()
                .CountAsync();

            int score = 100 - (offlineCount * 30) - (criticalAlertCount * 40);
            return Math.Max(0, Math.Min(100, score));
        }
    }

    public record CreatePropertyRequest(string Name, string? Description, PropertyType Type, string Country, string City, string Address, bool IsSecurityModeArmed);
    public record UpdatePropertyRequest(string Name, string? Description, PropertyType Type, string Country, string City, string Address, PropertyStatus Status, bool IsSecurityModeArmed);
}
