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
        private readonly ILandlordRepository _landlordRepository;     // un único campo (corregido)
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUserRepository userRepository,
            ILandlordRepository landlordRepository,
            ISubscriptionRepository subscriptionRepository,
            IUnitOfWork unitOfWork,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _landlordRepository = landlordRepository;                 // asignación correcta
            _subscriptionRepository = subscriptionRepository;
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

            // Obtener landlord (puede ser null si no se creó perfil)
            SubscriptionDto? subscriptionDto = null;

            // Atención: comprobar campo no nulo (landlordRepository debería estar registrado en DI)
            var landlord = await _landlordRepository.GetByUserIdAsync(user.Id);
            if (landlord != null && _subscriptionRepository != null)
            {
                var subscription = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
                if (subscription != null)
                {
                    subscriptionDto = new SubscriptionDto(
                        subscription.Id,
                        new SubscriptionPlanDto(
                            subscription.Plan.Id,
                            subscription.Plan.Name,
                            subscription.Plan.MonthlyPrice,
                            subscription.Plan.MaxPropertiesLimit,
                            subscription.Plan.UnlimitedProperties
                        ),
                        subscription.Status.ToString(),
                        subscription.StartedAt,
                        subscription.CurrentPeriodStart,
                        subscription.CurrentPeriodEnd,
                        subscription.CancelAtPeriodEnd
                    );
                }
            }

            var token = GenerateJwtToken(user);
            // Asegúrate de que AuthResponseDto acepta el parámetro Subscription (ver abajo)
            return new AuthResponseDto(user.Email, token, user.Id, subscriptionDto);
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto registerDto)
        {
            var existingUser = await _userRepository.GetByEmailAsync(registerDto.Email);
            if (existingUser != null) return null;

            var user = new User(
                registerDto.Email,
                BCrypt.Net.BCrypt.HashPassword(registerDto.Password)
            );

            await _userRepository.AddAsync(user);
            await _unitOfWork.SaveChangesAsync(); // Commit User to get ID

            var landlord = new Landlord(
                user.Id,
                registerDto.FirstName,
                registerDto.LastName,
                registerDto.Country,
                registerDto.City,
                registerDto.Address,
                registerDto.PhoneNumber
            );

            await _landlordRepository.AddAsync(landlord);
            await _unitOfWork.SaveChangesAsync(); // Commit Landlord

            var token = GenerateJwtToken(user);
            return new AuthResponseDto(user.Email, token, user.Id);
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
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["DurationInMinutes"]!)),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public async Task ChangePasswordAsync(long userId, string currentPassword, string newPassword)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ApplicationException("User not found.");

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                throw new ApplicationException("INVALID_PASSWORD"); // o lanzar custom exception

            user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(newPassword));
            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

    }
}
