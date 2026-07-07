using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Commands.Tenant;
using Nexora.Application.Dto;
using Nexora.Infrastructure.Persistence;
using System.Security.Claims;
using Nexora.Interface.DTOs;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

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
        public async Task<IActionResult> GetAll([FromQuery] long? propertyId = null)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            var query = _context.Tenants
                .Where(t => t.Property.Landlord.UserId == userId);

            if (propertyId.HasValue)
                query = query.Where(t => t.PropertyId == propertyId.Value);

            var tenants = await query
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

        [HttpPost("link")]
        public async Task<IActionResult> LinkTenant([FromBody] LinkTenantRequest request)
        {
            var userIdString = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !long.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var property = await _context.Properties
                .Include(p => p.Tenants)
                .FirstOrDefaultAsync(p => p.PropertyCode == request.PropertyCode);

            if (property == null)
            {
                return NotFound("Property code not found.");
            }

            var tenant = property.Tenants
                .FirstOrDefault(t => t.UserId == null && t.PhoneNumber == request.PhoneNumber);

            if (tenant == null)
            {
                return NotFound("No matching tenant record found for this property.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                if (user.UserableId.HasValue)
                {
                    var oldTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == user.UserableId.Value);
                    if (oldTenant != null && oldTenant.PropertyId == 1)
                    {
                        _context.Tenants.Remove(oldTenant);
                    }
                }
                
                tenant.LinkUser(userId);
                user.SetUserableProfile(tenant.Id);
            }
            else
            {
                tenant.LinkUser(userId);
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Successfully linked to property.", TenantId = tenant.Id });
        }
    }

    public record LinkTenantRequest(
        string PropertyCode,
        string PhoneNumber
    );

    public record UpdateTenantRequest(
        string FirstName,
        string LastName,
        string Country,
        string City,
        string Address,
        string? PhoneNumber
    );
}
