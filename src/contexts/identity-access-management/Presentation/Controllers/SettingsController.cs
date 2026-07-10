using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nexora.Application.Dto;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Persistence;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Domain.Resources;
using Swashbuckle.AspNetCore.Annotations;
using DomainUser = Nexora.Domain.Entities.User;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1")]
    [Authorize]
    [SwaggerTag("User Settings")]
    public class SettingsController : ControllerBase
    {
        private readonly NexoraDbContext _context;
        private readonly ILandlordRepository _landlordRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public SettingsController(
            NexoraDbContext context,
            ILandlordRepository landlordRepository,
            ITenantRepository tenantRepository,
            IUserRepository userRepository,
            IStringLocalizer<SharedMessages> localizer)
        {
            _context = context;
            _landlordRepository = landlordRepository;
            _tenantRepository = tenantRepository;
            _userRepository = userRepository;
            _localizer = localizer;
        }

        /// <summary>
        /// Returns the user's complete settings: available languages, notification preferences,
        /// account info, and security settings.
        /// </summary>
        [HttpGet("settings")]
        [SwaggerOperation(Summary = "Get settings", Description = "Returns complete settings: languages, notification preferences, account info, and security settings.")]
        [ProducesResponseType(typeof(SystemSettingsResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSettings()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId.Value);
            if (user == null) return Unauthorized();

            var userableType = User.FindFirst("userable_type")?.Value;
            string firstName = "", lastName = "", country = "", city = "";
            string? phoneNumber = null;

            if (userableType == "Landlord")
            {
                var landlord = await _landlordRepository.GetByUserIdAsync(userId.Value);
                if (landlord != null)
                {
                    firstName = landlord.FirstName;
                    lastName = landlord.LastName;
                    country = landlord.Country;
                    city = landlord.City;
                    phoneNumber = landlord.PhoneNumber;
                }
            }
            else if (userableType == "Tenant")
            {
                var userableIdStr = User.FindFirst("userable_id")?.Value;
                if (long.TryParse(userableIdStr, out var tenantId))
                {
                    var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                    if (tenant != null)
                    {
                        firstName = tenant.FirstName;
                        lastName = tenant.LastName;
                        country = tenant.Country;
                        city = tenant.City;
                        phoneNumber = tenant.PhoneNumber;
                    }
                }
            }

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
                    FirstName: firstName,
                    LastName: lastName,
                    Email: user.Email,
                    Country: country,
                    City: city,
                    PhoneNumber: phoneNumber
                ),
                Security: new SecuritySettingsDto(
                    Is2faActive: false,
                    LastPasswordChange: "Never"
                )
            ));
        }

        /// <summary>
        /// Updates the user's notification preferences (email and SMS alerts).
        /// </summary>
        [HttpPatch("notification-preferences")]
        [SwaggerOperation(Summary = "Update notification preferences", Description = "Sets email and SMS alert preferences for the authenticated user.")]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
            return Ok(new MessageResponse(_localizer["Settings_NotificationsUpdated"]));
        }

        /// <summary>
        /// Changes the user's password. Requires the current password for verification.
        /// </summary>
        [HttpPut("password")]
        [SwaggerOperation(Summary = "Change password", Description = "Updates the authenticated user's password after verifying the current password.")]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId.Value);
            if (user == null) return Unauthorized();

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                return BadRequest(new ErrorResponse("BadRequest", _localizer["Settings_PasswordIncorrect"]));

            var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatePassword(newHash);
            await _userRepository.UpdateAsync(user);
            await _context.SaveChangesAsync();

            return Ok(new MessageResponse(_localizer["Settings_PasswordUpdated"]));
        }

        private long? GetUserId()
        {
            return User.GetUserId();
        }
    }
}
