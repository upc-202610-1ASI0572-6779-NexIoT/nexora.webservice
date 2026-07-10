using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nexora.Application.Commands.Tenant;
using Nexora.Application.Dto;
using Nexora.Application.Services;
using Nexora.Infrastructure.Persistence;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Domain.Resources;
using Swashbuckle.AspNetCore.Annotations;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/tenants")]
    [Authorize]
    [SwaggerTag("Tenant Management")]
    public class TenantsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly NexoraDbContext _context;
        private readonly IAuthService _authService;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public TenantsController(IMediator mediator, NexoraDbContext context, IAuthService authService, IStringLocalizer<SharedMessages> localizer)
        {
            _mediator = mediator;
            _context = context;
            _authService = authService;
            _localizer = localizer;
        }

        [HttpPost]
        [SwaggerOperation(Summary = "Create a tenant", Description = "Creates a new tenant record linked to a property owned by the landlord.")]
        [ProducesResponseType(typeof(long), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create([FromBody] CreateTenantDto request)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var propertyOwned = await _context.Properties
                .AnyAsync(p => p.Id == request.PropertyId && p.Landlord.UserId == userId.Value);
            if (!propertyOwned)
                return NotFound(new ErrorResponse("NotFound", _localizer["Property_NotOwned"]));

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
            return CreatedAtAction(nameof(GetById), new { tenantId = id }, id);
        }

        [HttpGet("{tenantId}")]
        [SwaggerOperation(Summary = "Get tenant by ID", Description = "Returns detailed information for a specific tenant.")]
        [ProducesResponseType(typeof(TenantDetailResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long tenantId)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var tenant = await _context.Tenants
                .Where(t => t.Id == tenantId && t.Property.Landlord.UserId == userId.Value)
                .Select(t => new TenantDetailResponseDto(
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
                .FirstOrDefaultAsync();

            if (tenant == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Tenant_NotFoundOrNotOwned"]));
            return Ok(tenant);
        }

        [HttpGet]
        [SwaggerOperation(Summary = "List tenants", Description = "Returns all tenants for the landlord, optionally filtered by property ID.")]
        [ProducesResponseType(typeof(List<TenantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll([FromQuery] long? propertyId = null)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var query = _context.Tenants
                .Where(t => t.Property.Landlord.UserId == userId.Value);

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

        [HttpPatch("{tenantId}")]
        [SwaggerOperation(Summary = "Update a tenant", Description = "Updates personal information for a specific tenant.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long tenantId, [FromBody] UpdateTenantRequest request)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var owned = await _context.Tenants
                .AnyAsync(t => t.Id == tenantId && t.Property.Landlord.UserId == userId.Value);
            if (!owned)
                return NotFound(new ErrorResponse("NotFound", _localizer["Tenant_NotFoundOrNotOwned"]));

            var cmd = new UpdateTenantCommand(
                tenantId,
                request.FirstName,
                request.LastName,
                request.Country,
                request.City,
                request.Address,
                request.PhoneNumber
            );

            var result = await _mediator.Send(cmd);
            if (!result)
                return NotFound(new ErrorResponse("NotFound", _localizer["Tenant_NotFoundOrNotOwned"]));
            return NoContent();
        }

        [HttpDelete("{tenantId}")]
        [SwaggerOperation(Summary = "Delete a tenant", Description = "Removes a tenant record.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long tenantId)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var owned = await _context.Tenants
                .AnyAsync(t => t.Id == tenantId && t.Property.Landlord.UserId == userId.Value);
            if (!owned)
                return NotFound(new ErrorResponse("NotFound", _localizer["Tenant_NotFoundOrNotOwned"]));

            var cmd = new DeleteTenantCommand(tenantId);
            var result = await _mediator.Send(cmd);
            if (!result)
                return NotFound(new ErrorResponse("NotFound", _localizer["Tenant_NotFoundOrNotOwned"]));
            return NoContent();
        }

        [HttpPost("~/api/v1/tenancies")]
        [SwaggerOperation(Summary = "Link tenant to property", Description = "Links the authenticated user to a tenant record using property code and phone number. Returns a new JWT token.")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LinkTenant([FromBody] LinkTenantRequest request)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var property = await _context.Properties
                .Include(p => p.Tenants)
                .FirstOrDefaultAsync(p => p.PropertyCode == request.PropertyCode);

            if (property == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Tenant_PropertyCodeNotFound"]));

            var tenant = property.Tenants
                .FirstOrDefault(t => t.UserId == null && t.PhoneNumber == request.PhoneNumber);

            if (tenant == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Tenant_NoMatchingRecord"]));

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (user == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            if (user.UserableId.HasValue)
            {
                var oldTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == user.UserableId.Value);
                if (oldTenant != null && !oldTenant.PropertyId.HasValue)
                {
                    _context.Tenants.Remove(oldTenant);
                }
            }

            tenant.LinkUser(userId.Value);
            user.SetUserableProfile(tenant.Id);

            await _context.SaveChangesAsync();

            var token = _authService.GenerateJwtToken(user);

            return Ok(new AuthResponseDto(
                user.Email,
                token,
                user.Id,
                user.UserableType ?? "Tenant",
                tenant.Id
            ));
        }

        /// <summary>
        /// Returns all tenancies. Filter by tenantId or propertyId.
        /// </summary>
        [HttpGet("~/api/v1/tenancies")]
        [SwaggerOperation(Summary = "List tenancies", Description = "Returns all tenant-property links, optionally filtered by tenant or property.")]
        [ProducesResponseType(typeof(List<TenancyDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetTenancies([FromQuery] long? tenantId = null, [FromQuery] long? propertyId = null)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var query = _context.Tenants
                .Where(t => t.Property.Landlord.UserId == userId.Value);

            if (tenantId.HasValue)
                query = query.Where(t => t.Id == tenantId.Value);

            if (propertyId.HasValue)
                query = query.Where(t => t.PropertyId == propertyId.Value);

            var tenancies = await query
                .Select(t => new TenancyDto(
                    t.Id,
                    t.Id,
                    t.PropertyId!.Value,
                    t.UserId,
                    t.CreatedAt
                ))
                .ToListAsync();

            return Ok(tenancies);
        }

        /// <summary>
        /// Returns a specific tenancy by ID.
        /// </summary>
        [HttpGet("~/api/v1/tenancies/{tenancyId:long}")]
        [SwaggerOperation(Summary = "Get tenancy by ID", Description = "Returns details for a specific tenant-property link.")]
        [ProducesResponseType(typeof(TenancyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTenancyById(long tenancyId)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var tenancy = await _context.Tenants
                .Where(t => t.Id == tenancyId && t.Property.Landlord.UserId == userId.Value)
                .Select(t => new TenancyDto(
                    t.Id,
                    t.Id,
                    t.PropertyId!.Value,
                    t.UserId,
                    t.CreatedAt
                ))
                .FirstOrDefaultAsync();

            if (tenancy == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Tenant_NotFoundOrNotOwned"]));

            return Ok(tenancy);
        }

        /// <summary>
        /// Deletes a tenancy (unlinks tenant from property).
        /// </summary>
        [HttpDelete("~/api/v1/tenancies/{tenancyId:long}")]
        [SwaggerOperation(Summary = "Delete tenancy", Description = "Removes a tenant-property link.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTenancy(long tenancyId)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var tenancy = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenancyId && t.Property.Landlord.UserId == userId.Value);

            if (tenancy == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Tenant_NotFoundOrNotOwned"]));

            _context.Tenants.Remove(tenancy);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public record LinkTenantRequest(string PropertyCode, string PhoneNumber);
    public record UpdateTenantRequest(string FirstName, string LastName, string Country, string City, string Address, string? PhoneNumber);
    public record TenancyDto(long Id, long TenantId, long PropertyId, long? UserId, DateTime CreatedAt);
}
