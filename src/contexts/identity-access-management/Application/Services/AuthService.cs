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
        private readonly ILandlordRepository _landlordRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUserRepository userRepository,
            ILandlordRepository landlordRepository,
            ITenantRepository tenantRepository,
            IUnitOfWork unitOfWork,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _landlordRepository = landlordRepository;
            _tenantRepository = tenantRepository;
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto?> RegisterLandlordAsync(RegisterLandlordDto dto)
        {
            var existingUser = await _userRepository.GetByEmailAsync(dto.Email);
            if (existingUser != null) return null;

            var user = new User(
                dto.Email,
                BCrypt.Net.BCrypt.HashPassword(dto.Password),
                User.LandlordType
            );

            await _userRepository.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var landlord = new Landlord(
                user.Id,
                dto.FirstName,
                dto.LastName,
                dto.Country,
                dto.City,
                dto.Address,
                dto.PhoneNumber
            );

            await _landlordRepository.AddAsync(landlord);
            await _unitOfWork.SaveChangesAsync();

            user.SetUserableProfile(landlord.Id);
            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return new AuthResponseDto(user.Email, token, user.Id, User.LandlordType, landlord.Id);
        }

        public async Task<AuthResponseDto?> RegisterTenantAsync(RegisterTenantDto dto)
        {
            var existingUser = await _userRepository.GetByEmailAsync(dto.Email);
            if (existingUser != null) return null;

            var user = new User(
                dto.Email,
                BCrypt.Net.BCrypt.HashPassword(dto.Password),
                User.TenantType
            );

            await _userRepository.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            Tenant tenant;
            if (dto.ExistingTenantId.HasValue)
            {
                tenant = await _tenantRepository.GetByIdAsync(dto.ExistingTenantId.Value);
                if (tenant == null) return null;
                tenant.LinkUser(user.Id);
                await _tenantRepository.UpdateAsync(tenant);
            }
            else
            {
                tenant = new Tenant(
                    dto.PropertyId,
                    dto.FirstName,
                    dto.LastName,
                    dto.Country,
                    dto.City,
                    dto.Address,
                    dto.PhoneNumber,
                    user.Id
                );

                await _tenantRepository.AddAsync(tenant);
            }

            await _unitOfWork.SaveChangesAsync();

            user.SetUserableProfile(tenant.Id);
            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return new AuthResponseDto(user.Email, token, user.Id, User.TenantType, tenant.Id);
        }

        public async Task<AuthResponseDto?> LoginWebAsync(LoginDto loginDto)
        {
            var user = await _userRepository.GetByEmailAsync(loginDto.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
                return null;

            if (user.UserableType != User.LandlordType || user.UserableId == null)
                throw new ForbiddenAccessException("Acceso denegado. Esta plataforma es exclusiva para arrendadores.");

            var token = GenerateJwtToken(user);

            return new AuthResponseDto(user.Email, token, user.Id, user.UserableType, user.UserableId.Value);
        }

        public async Task<AuthResponseDto?> LoginMobileAsync(LoginDto loginDto)
        {
            var user = await _userRepository.GetByEmailAsync(loginDto.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
                return null;

            if (user.UserableType != User.TenantType || user.UserableId == null)
                throw new ForbiddenAccessException("Acceso denegado. Esta plataforma es exclusiva para arrendatarios.");

            var token = GenerateJwtToken(user);

            return new AuthResponseDto(user.Email, token, user.Id, user.UserableType, user.UserableId.Value);
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
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("userable_type", user.UserableType ?? ""),
                    new Claim("userable_id", user.UserableId?.ToString() ?? "")
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