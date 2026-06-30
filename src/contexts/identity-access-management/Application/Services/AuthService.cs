using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Nexora.Application.Dto;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Nexora.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
        {
            var user = await _userRepository.GetByEmailAsync(loginDto.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                return null;
            }

            var token = GenerateJwtToken(user);

            return new AuthResponseDto(user.Email, token, user.Id);
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto)
        {
            var existingUser = await _userRepository.GetByEmailAsync(registerDto.Email);

            if (existingUser != null)
            {
                return null;
            }

            var user = new User(
                registerDto.Email,
                BCrypt.Net.BCrypt.HashPassword(registerDto.Password)
            );

            await _userRepository.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return new AuthResponseDto(user.Email, token, user.Id);
        }

        public async Task ChangePasswordAsync(
            long userId,
            string currentPassword,
            string newPassword)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
            {
                throw new ApplicationException("User not found.");
            }

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                throw new ApplicationException("INVALID_PASSWORD");
            }

            user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(newPassword));

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email)
                }),
                Expires = DateTime.UtcNow.AddMinutes(
                    double.Parse(jwtSettings["DurationInMinutes"]!)
                ),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}