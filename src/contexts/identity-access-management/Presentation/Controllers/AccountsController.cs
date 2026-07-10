using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nexora.Application.Dto;
using Nexora.Application.Services;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Domain.Resources;
using Swashbuckle.AspNetCore.Annotations;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [SwaggerTag("Accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public AccountsController(IAuthService authService, IStringLocalizer<SharedMessages> localizer)
        {
            _authService = authService;
            _localizer = localizer;
        }

        /// <summary>
        /// Registers a new landlord account with email, password, and personal details.
        /// Returns a JWT token on success.
        /// </summary>
        [HttpPost("api/v1/landlord-accounts")]
        [SwaggerOperation(Summary = "Register a new landlord", Description = "Creates a landlord account with personal information and returns a JWT access token.")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RegisterLandlord([FromBody] RegisterLandlordDto dto)
        {
            var response = await _authService.RegisterLandlordAsync(dto);
            if (response == null)
                return Conflict(new ErrorResponse("Conflict", _localizer["Auth_EmailAlreadyRegistered"]));
            return StatusCode(201, response);
        }

        /// <summary>
        /// Registers a new tenant account. The tenant must be pre-created by a landlord.
        /// Returns a JWT token on success.
        /// </summary>
        [HttpPost("api/v1/tenant-accounts")]
        [SwaggerOperation(Summary = "Register a new tenant", Description = "Creates a tenant account linked to a property. The tenant record must already exist (created by the landlord).")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantDto dto)
        {
            var response = await _authService.RegisterTenantAsync(dto);
            if (response == null)
                return Conflict(new ErrorResponse("Conflict", _localizer["Auth_EmailOrTenantNotFound"]));
            return StatusCode(201, response);
        }
    }
}
