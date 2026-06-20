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
    [Route("api/v1/settings")]
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

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId.Value);
            if (user == null) return Unauthorized();

            var landlord = await _landlordRepository.GetByUserIdAsync(userId.Value);

            var prefs = await _context.NotificationPreferences
                .FirstOrDefaultAsync(n => n.UserId == userId.Value);

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

        [HttpPut("language")]
        public async Task<IActionResult> UpdateLanguage([FromBody] UpdateLanguageRequest request)
        {
            await Task.CompletedTask;
            return Ok(new { message = "Language updated successfully." });
        }

        [HttpPut("notifications")]
        public async Task<IActionResult> UpdateNotifications([FromBody] UpdateNotificationRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var prefs = await _context.NotificationPreferences
                .FirstOrDefaultAsync(n => n.UserId == userId.Value);

            if (prefs == null)
            {
                prefs = new NotificationPreference(userId.Value, request.EmailAlerts, request.SmsAlerts);
                _context.NotificationPreferences.Add(prefs);
            }
            else
            {
                prefs.UpdatePreferences(request.EmailAlerts, request.SmsAlerts);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Notification preferences updated." });
        }

        [HttpPut("security/passwords")]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId.Value);
            if (user == null) return Unauthorized();

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                return BadRequest(new { message = "Current password is incorrect." });

            var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatePassword(newHash);
            await _userRepository.UpdateAsync(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password updated successfully." });
        }

        [HttpPut("security/two-factor")]
        public async Task<IActionResult> Toggle2fa([FromBody] Toggle2faRequest request)
        {
            await Task.CompletedTask;
            return Ok(new { is2faActive = request.Enable });
        }

        private long? GetUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return null;
            return userId;
        }
    }

    public record Toggle2faRequest(bool Enable);
}
