using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Commands.Tenant;
using Nexora.Application.Dto;
using Nexora.Infrastructure.Persistence;
using System.Security.Claims;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/tenants")]
    [Authorize]
    public class TenantsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly NexoraDbContext _context;

        public TenantsController(IMediator mediator, NexoraDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTenantDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var propertyOwned = await _context.Properties
                .AnyAsync(p => p.Id == request.PropertyId && p.Landlord.UserId == userId);
            if (!propertyOwned) return NotFound("Property not found or not owned by current user.");

            var cmd = new CreateTenantCommand(
                request.PropertyId,
                request.FirstName,
                request.LastName,
                request.Country,
                request.City,
                request.Address,
                request.PhoneNumber
            );
            var id = await _mediator.Send(cmd);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var tenant = await _context.Tenants
                .Where(t => t.Id == id && t.Property.Landlord.UserId == userId)
                .Select(t => new
                {
                    t.Id,
                    t.PropertyId,
                    t.UserId,
                    t.FirstName,
                    t.LastName,
                    t.Country,
                    t.City,
                    t.Address,
                    t.PhoneNumber,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (tenant == null) return NotFound();
            return Ok(tenant);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var tenants = await _context.Tenants
                .Where(t => t.Property.Landlord.UserId == userId)
                .Select(t => new TenantDto(
                    t.Id,
                    t.PropertyId,
                    t.UserId,
                    t.FirstName,
                    t.LastName,
                    t.Country,
                    t.City,
                    t.Address,
                    t.PhoneNumber,
                    t.CreatedAt,
                    t.UpdatedAt
                ))
                .ToListAsync();

            return Ok(tenants);
        }

        [HttpGet("/api/v1/properties/{propertyId}/tenants")]
        public async Task<IActionResult> GetByProperty(long propertyId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var tenants = await _context.Tenants
                .Where(t => t.PropertyId == propertyId && t.Property.Landlord.UserId == userId)
                .Select(t => new TenantDto(
                    t.Id,
                    t.PropertyId,
                    t.UserId,
                    t.FirstName,
                    t.LastName,
                    t.Country,
                    t.City,
                    t.Address,
                    t.PhoneNumber,
                    t.CreatedAt,
                    t.UpdatedAt
                ))
                .ToListAsync();

            return Ok(tenants);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateTenantRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var owned = await _context.Tenants
                .AnyAsync(t => t.Id == id && t.Property.Landlord.UserId == userId);
            if (!owned) return NotFound();

            var cmd = new UpdateTenantCommand(
                id,
                request.FirstName,
                request.LastName,
                request.Country,
                request.City,
                request.Address,
                request.PhoneNumber
            );

            var result = await _mediator.Send(cmd);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var owned = await _context.Tenants
                .AnyAsync(t => t.Id == id && t.Property.Landlord.UserId == userId);
            if (!owned) return NotFound();

            var cmd = new DeleteTenantCommand(id);
            var result = await _mediator.Send(cmd);
            if (!result) return NotFound();
            return NoContent();
        }
    }

    public record UpdateTenantRequest(
        string FirstName,
        string LastName,
        string Country,
        string City,
        string Address,
        string? PhoneNumber
    );
}
