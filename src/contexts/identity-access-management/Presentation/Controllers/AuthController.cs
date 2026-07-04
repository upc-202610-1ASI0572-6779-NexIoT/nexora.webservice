using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexora.Application.Dto;
using Nexora.Application.Services;
using System.Security.Claims;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register/landlords")]
        public async Task<IActionResult> RegisterLandlord([FromBody] RegisterLandlordDto dto)
        {
            var response = await _authService.RegisterLandlordAsync(dto);
            if (response == null)
                return Conflict(new ErrorResponseDto("Conflict", "El correo electrónico ya está registrado."));
            return StatusCode(201, response);
        }

        [HttpPost("register/tenants")]
        public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantDto dto)
        {
            var response = await _authService.RegisterTenantAsync(dto);
            if (response == null)
                return Conflict(new ErrorResponseDto("Conflict", "El correo electrónico ya está registrado o el arrendatario no existe."));
            return StatusCode(201, response);
        }

        [HttpPost("login/web")]
        public async Task<IActionResult> LoginWeb([FromBody] LoginDto loginDto)
        {
            try
            {
                var response = await _authService.LoginWebAsync(loginDto);
                if (response == null)
                    return Unauthorized(new ErrorResponseDto("Unauthorized", "Credenciales inválidas."));
                return Ok(response);
            }
            catch (ForbiddenAccessException ex)
            {
                return StatusCode(403, new ErrorResponseDto("Forbidden", ex.Message));
            }
        }

        [HttpPost("login/mobile")]
        public async Task<IActionResult> LoginMobile([FromBody] LoginDto loginDto)
        {
            try
            {
                var response = await _authService.LoginMobileAsync(loginDto);
                if (response == null)
                    return Unauthorized(new ErrorResponseDto("Unauthorized", "Credenciales inválidas."));
                return Ok(response);
            }
            catch (ForbiddenAccessException ex)
            {
                return StatusCode(403, new ErrorResponseDto("Forbidden", ex.Message));
            }
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return Unauthorized();

            try
            {
                await _authService.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
                return Ok(new { message = "Password changed" });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { code = "INVALID_PASSWORD", message = ex.Message });
            }
        }
    }
}
