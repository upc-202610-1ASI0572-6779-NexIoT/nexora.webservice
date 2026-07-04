using Nexora.Application.Dto;

namespace Nexora.Application.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> RegisterLandlordAsync(RegisterLandlordDto dto);
        Task<AuthResponseDto?> RegisterTenantAsync(RegisterTenantDto dto);
        Task<AuthResponseDto?> LoginWebAsync(LoginDto loginDto);
        Task<AuthResponseDto?> LoginMobileAsync(LoginDto loginDto);
        Task ChangePasswordAsync(long userId, string currentPassword, string newPassword);
    }
}
