using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexora.Application.Commands.Property;
using Nexora.Domain.Enums;
using Nexora.Infrastructure.Persistence;
using Nexora.WebApi.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/properties")]
    [Authorize]
    public class PropertiesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly NexoraDbContext _context;

        public PropertiesController(IMediator mediator, NexoraDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePropertyRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

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
                    userId
                );
                var id = await _mediator.Send(command);
                return CreatedAtAction(nameof(GetById), new { id }, id);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(long id, [FromBody] PropertyStatus status)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var owned = await _context.Properties
                .AnyAsync(p => p.Id == id && p.Landlord.UserId == userId);
            if (!owned) return NotFound();

            var command = new UpdatePropertyStatusCommand(id, status);
            var result = await _mediator.Send(command);
            if (!result) return NotFound();
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

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? code = null)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            if (!string.IsNullOrEmpty(code))
            {
                var property = await _context.Properties
                    .Include(p => p.Landlord)
                    .FirstOrDefaultAsync(p => p.PropertyCode == code && p.Landlord.UserId == userId);

                if (property == null) return NotFound();

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
                .Where(p => p.Landlord.UserId == userId)
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var property = await _context.Properties
                .Include(p => p.Landlord)
                .FirstOrDefaultAsync(p => p.Id == id && p.Landlord.UserId == userId);

            if (property == null) return NotFound();

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

        // --- Mantiene el endpoint /summary ---
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var total = await _context.Properties
                .CountAsync(p => p.Landlord.UserId == userId);

            var protectedCount = await _context.Properties.CountAsync(p =>
                p.Landlord.UserId == userId &&
                p.Status == PropertyStatus.ACTIVE &&
                p.IsSecurityModeArmed);

            return Ok(new { Total = total, ProtectedCount = protectedCount });
        }

        // --- Mantiene el endpoint /stats ---
        [HttpGet("stats")]
        public async Task<IActionResult> GetTotalProperties()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var total = await _context.Properties
                .CountAsync(p => p.Landlord.UserId == userId);
            return Ok(new { Total = total });
        }

        // --- Mantiene el endpoint /dashboards ---
        [HttpGet("dashboards")]
        public async Task<IActionResult> GetEmptyAndProtectedCount()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var count = await _context.Properties.CountAsync(p => 
                p.Landlord.UserId == userId && 
                p.Status == PropertyStatus.ACTIVE &&
                p.IsSecurityModeArmed);
            
            return Ok(new { Count = count });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdatePropertyRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var owned = await _context.Properties
                .AnyAsync(p => p.Id == id && p.Landlord.UserId == userId);
            if (!owned) return NotFound();

            var command = new UpdatePropertyCommand(
                id,
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
            if (!result) return NotFound();
            return NoContent();
        }
    }

    public record CreatePropertyRequest(
        string Name, 
        string? Description,
        PropertyType Type,
        string Country,
        string City,
        string Address,
        bool IsSecurityModeArmed
    );

    public record UpdatePropertyRequest(
        string Name,
        string? Description,
        PropertyType Type,
        string Country,
        string City,
        string Address,
        PropertyStatus Status,
        bool IsSecurityModeArmed
    );
}
