using Microsoft.AspNetCore.Authorization;
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
    [SwaggerTag("Sessions")]
    public class SessionsController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public SessionsController(IAuthService authService, IStringLocalizer<SharedMessages> localizer)
        {
            _authService = authService;
            _localizer = localizer;
        }

        /// <summary>
        /// Authenticates a user with email and password. Returns a JWT access token
        /// that must be included in subsequent requests as: Authorization: Bearer {token}
        /// </summary>
        [HttpPost("api/v1/sessions")]
        [SwaggerOperation(Summary = "Authenticate user", Description = "Validates credentials and returns a JWT token. Use the token in the Authorization header for authenticated endpoints.")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] LoginDto loginDto)
        {
            try
            {
                var response = await _authService.LoginAsync(loginDto);
                if (response == null)
                    return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_InvalidCredentials"]));
                return Ok(response);
            }
            catch (ForbiddenAccessException ex)
            {
                return StatusCode(403, new ErrorResponse("Forbidden", ex.Message));
            }
        }

        /// <summary>
        /// Ends the current session. Since JWTs are stateless, this is a no-op
        /// that confirms the session has ended. The client should discard the token.
        /// </summary>
        [Authorize]
        [HttpDelete("api/v1/sessions/current")]
        [SwaggerOperation(Summary = "End session (logout)", Description = "Discards the current JWT token. Client-side only — the token is not persisted server-side.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public IActionResult Delete()
        {
            return NoContent();
        }
    }
}
