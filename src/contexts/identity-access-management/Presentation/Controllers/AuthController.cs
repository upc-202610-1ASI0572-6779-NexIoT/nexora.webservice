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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                var response = await _authService.LoginAsync(loginDto);
                if (response == null)
                    return Unauthorized(new ErrorResponseDto("Unauthorized", "Credenciales inválidas."));
                return Ok(response);
            }
            catch (ForbiddenAccessException ex)
            {
                return StatusCode(403, new ErrorResponseDto("Forbidden", ex.Message));
            }
        }
    }
}
