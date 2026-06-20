using Nexora.Application.Dto;
using System.Threading.Tasks;

namespace Nexora.Application.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(LoginDto loginDto);
        Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto);
        Task ChangePasswordAsync(long userId, string currentPassword, string newPassword);
    }
}
