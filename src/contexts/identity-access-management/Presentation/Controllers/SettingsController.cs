using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Dto;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;
using System.Security.Claims;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly NexoraDbContext _context;
        private readonly ILandlordRepository _landlordRepository;
        private readonly IUserRepository _userRepository;

        public SettingsController(
            NexoraDbContext context,
            ILandlordRepository landlordRepository,
            IUserRepository userRepository)
        {
            _context = context;
            _landlordRepository = landlordRepository;
            _userRepository = userRepository;
        }

        [HttpGet("users/{userId}/settings")]
        public async Task<IActionResult> GetUserSettings(long userId)
        {
            var loggedInUserId = GetUserId();
            if (loggedInUserId == null || loggedInUserId.Value != userId) return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return Unauthorized();

            var landlord = await _landlordRepository.GetByUserIdAsync(userId);

            var prefs = await _context.NotificationPreferences
                .FirstOrDefaultAsync(n => n.UserId == userId);

            return Ok(new SystemSettingsResponseDto(
                Languages: new[]
                {
                    new LanguageDto("es", "Español", true),
                    new LanguageDto("en", "English", false)
                },
                Notifications: new NotificationPreferencesDto(
                    EmailAlerts: prefs?.ReceiveEmailAlerts ?? true,
                    SmsAlerts: prefs?.ReceiveSmsAlerts ?? false,
                    PushAlerts: true
                ),
                Account: new AccountInfoDto(
                    FirstName: landlord?.FirstName ?? "",
                    LastName: landlord?.LastName ?? "",
                    Email: user.Email,
                    Country: landlord?.Country ?? "",
                    City: landlord?.City ?? "",
                    PhoneNumber: landlord?.PhoneNumber
                ),
                Security: new SecuritySettingsDto(
                    Is2faActive: false,
                    LastPasswordChange: "Never"
                )
            ));
        }

        [HttpPut("users/{userId}/settings")]
        public async Task<IActionResult> UpdateSettings(long userId, [FromBody] UpdateSettingsRequest request)
        {
            var loggedInUserId = GetUserId();
            if (loggedInUserId == null || loggedInUserId.Value != userId) return Unauthorized();

            var prefs = await _context.NotificationPreferences
                .FirstOrDefaultAsync(n => n.UserId == userId);

            if (prefs == null)
            {
                prefs = new NotificationPreference(userId, request.EmailAlerts, request.SmsAlerts);
                _context.NotificationPreferences.Add(prefs);
            }
            else
            {
                prefs.UpdatePreferences(request.EmailAlerts, request.SmsAlerts);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Settings updated successfully." });
        }

        private long? GetUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return null;
            return userId;
        }
    }
}
