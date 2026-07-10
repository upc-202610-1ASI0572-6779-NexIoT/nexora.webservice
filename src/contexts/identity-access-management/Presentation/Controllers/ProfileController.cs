using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Nexora.Application.Dto;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Repositories;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Domain.Resources;
using Swashbuckle.AspNetCore.Annotations;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/profile")]
    [SwaggerTag("User Profile")]
    public class ProfileController : ControllerBase
    {
        private readonly ILandlordRepository _landlordRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<SharedMessages> _localizer;

        public ProfileController(
            ILandlordRepository landlordRepository,
            ITenantRepository tenantRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IStringLocalizer<SharedMessages> localizer)
        {
            _landlordRepository = landlordRepository;
            _tenantRepository = tenantRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _localizer = localizer;
        }

        /// <summary>
        /// Returns the authenticated user's profile. The response includes the profile data
        /// and a type field indicating whether the user is a Landlord or Tenant.
        /// </summary>
        [Authorize]
        [HttpGet]
        [SwaggerOperation(Summary = "Get current user profile", Description = "Retrieves the profile of the authenticated user.")]
        [ProducesResponseType(typeof(ProfileResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCurrent()
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var userableType = User.FindFirst("userable_type")?.Value;
            var user = await _userRepository.GetByIdAsync(userId.Value);

            if (user == null)
                return NotFound(new ErrorResponse("NotFound", _localizer["Profile_NotFound"]));

            if (userableType == "Landlord")
            {
                var landlord = await _landlordRepository.GetByUserIdAsync(userId.Value);
                if (landlord == null)
                    return NotFound(new ErrorResponse("NotFound", _localizer["Profile_LandlordNotFound"]));

                var dto = new ProfileDto(
                    user.Email,
                    landlord.FirstName,
                    landlord.LastName,
                    user.IsActive,
                    landlord.Country,
                    landlord.City,
                    landlord.Address,
                    landlord.PhoneNumber
                );

                return Ok(new ProfileResponseDto(dto, "Landlord", null));
            }
            else if (userableType == "Tenant")
            {
                var userableIdStr = User.FindFirst("userable_id")?.Value;
                if (!long.TryParse(userableIdStr, out var tenantId))
                    return NotFound(new ErrorResponse("NotFound", _localizer["Profile_TenantNotFound"]));

                var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                if (tenant == null)
                    return NotFound(new ErrorResponse("NotFound", _localizer["Profile_TenantNotFound"]));

                var dto = new ProfileDto(
                    user.Email,
                    tenant.FirstName,
                    tenant.LastName,
                    user.IsActive,
                    tenant.Country,
                    tenant.City,
                    tenant.Address,
                    tenant.PhoneNumber
                );

                return Ok(new ProfileResponseDto(dto, "Tenant", null));
            }

            return NotFound(new ErrorResponse("NotFound", _localizer["Profile_NotFound"]));
        }

        /// <summary>
        /// Updates the authenticated user's personal information (name, location, phone).
        /// Returns the updated profile on success.
        /// </summary>
        [Authorize]
        [HttpPatch]
        [SwaggerOperation(Summary = "Update current user profile", Description = "Updates personal information for the authenticated user.")]
        [ProducesResponseType(typeof(ProfileResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update([FromBody] UpdateProfileDto update)
        {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            var userableType = User.FindFirst("userable_type")?.Value;
            var user = await _userRepository.GetByIdAsync(userId.Value);
            if (user == null)
                return Unauthorized(new ErrorResponse("Unauthorized", _localizer["Auth_Unauthorized"]));

            if (userableType == "Landlord")
            {
                var landlord = await _landlordRepository.GetByUserIdAsync(userId.Value);
                if (landlord == null)
                    return BadRequest(new ErrorResponse("BadRequest", _localizer["Profile_LandlordNotFound"]));

                landlord.UpdatePersonalInfo(update.FirstName, update.LastName, update.Country, update.City, update.Address, update.PhoneNumber);

                await _landlordRepository.UpdateAsync(landlord);
                await _unitOfWork.SaveChangesAsync();

                var dto = new ProfileDto(
                    user.Email,
                    landlord.FirstName,
                    landlord.LastName,
                    user.IsActive,
                    landlord.Country,
                    landlord.City,
                    landlord.Address,
                    landlord.PhoneNumber
                );

                return Ok(new ProfileResponseDto(dto, "Landlord", null));
            }
            else if (userableType == "Tenant")
            {
                var userableIdStr = User.FindFirst("userable_id")?.Value;
                if (!long.TryParse(userableIdStr, out var tenantId))
                    return BadRequest(new ErrorResponse("BadRequest", _localizer["Profile_TenantNotFound"]));

                var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                if (tenant == null)
                    return BadRequest(new ErrorResponse("BadRequest", _localizer["Profile_TenantNotFound"]));

                tenant.UpdatePersonalInfo(update.FirstName, update.LastName, update.Country, update.City, update.Address, update.PhoneNumber);

                await _tenantRepository.UpdateAsync(tenant);
                await _unitOfWork.SaveChangesAsync();

                var dto = new ProfileDto(
                    user.Email,
                    tenant.FirstName,
                    tenant.LastName,
                    user.IsActive,
                    tenant.Country,
                    tenant.City,
                    tenant.Address,
                    tenant.PhoneNumber
                );

                return Ok(new ProfileResponseDto(dto, "Tenant", null));
            }

            return BadRequest(new ErrorResponse("BadRequest", _localizer["Profile_UnknownUserType"]));
        }
    }
}
