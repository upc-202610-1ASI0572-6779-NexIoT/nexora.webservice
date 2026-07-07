using Nexora.Domain.Entities;
using Nexora.Application.Dto;

namespace Nexora.Application.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> RegisterLandlordAsync(RegisterLandlordDto dto);
        Task<AuthResponseDto?> RegisterTenantAsync(RegisterTenantDto dto);
        Task<AuthResponseDto?> LoginAsync(LoginDto loginDto);
        Task ChangePasswordAsync(long userId, string currentPassword, string newPassword);
        string GenerateJwtToken(User user);
    }
}
