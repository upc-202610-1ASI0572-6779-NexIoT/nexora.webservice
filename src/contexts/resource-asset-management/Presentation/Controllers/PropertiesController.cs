using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexora.Application.Commands.Property;
using Nexora.Domain.Enums;
using Nexora.Infrastructure.Persistence;
using Nexora.WebApi.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(long id, [FromBody] PropertyStatus status)
        {
            var command = new UpdatePropertyStatusCommand(id, status);
            var result = await _mediator.Send(command);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? code = null)
        {
            if (!string.IsNullOrEmpty(code))
            {
                var property = await _context.Properties
                    .Where(p => p.PropertyCode == code)
                    .Select(p => new PropertyDto(
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
                        )
                    ))
                    .FirstOrDefaultAsync();

                if (property == null) return NotFound();
                return Ok(property);
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var properties = await _context.Properties
                .Where(p => p.Landlord.UserId == userId)
                .Select(p => new PropertyDto(
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
                    )
                ))
                .ToListAsync();

            return Ok(properties);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var property = await _context.Properties
                .Where(p => p.Id == id)
                .Select(p => new PropertyDto(
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
                    )
                ))
                .FirstOrDefaultAsync();

            if (property == null) return NotFound();
            return Ok(property);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetTotalProperties()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var total = await _context.Properties
                .CountAsync(p => p.Landlord.UserId == userId);
            return Ok(new { Total = total });
        }

        [HttpGet("dashboards")]
        public async Task<IActionResult> GetEmptyAndProtectedCount()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var count = await _context.Properties.CountAsync(p => 
                p.Landlord.UserId == userId && 
                p.Status == PropertyStatus.ACTIVE && // "ACTIVE" means available/empty in this context? 
                p.IsSecurityModeArmed);
            
            return Ok(new { Count = count });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdatePropertyRequest request)
        {
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
