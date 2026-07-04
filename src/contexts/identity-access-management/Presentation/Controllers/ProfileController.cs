using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexora.Application.Dto;
using Nexora.Domain.Repositories;
using Nexora.Infrastructure.Repositories;
using System.Security.Claims;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/profiles")]
    public class ProfileController : ControllerBase
    {
        private readonly ILandlordRepository _landlordRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ProfileController(
            ILandlordRepository landlordRepository,
            ITenantRepository tenantRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork)
        {
            _landlordRepository = landlordRepository;
            _tenantRepository = tenantRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrent()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdStr, out var userId)) return Unauthorized();

            var userableType = User.FindFirstValue("userable_type");
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
                return Ok(new { profile = (object?)null, message = "No profile found." });

            if (userableType == "Landlord")
            {
                var landlord = await _landlordRepository.GetByUserIdAsync(userId);
                if (landlord == null)
                    return Ok(new { profile = (object?)null, message = "No profile found." });

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

                return Ok(new { profile = dto, type = "Landlord" });
            }
            else if (userableType == "Tenant")
            {
                var userableIdStr = User.FindFirstValue("userable_id");
                if (!long.TryParse(userableIdStr, out var tenantId))
                    return Ok(new { profile = (object?)null, message = "No profile found." });

                var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                if (tenant == null)
                    return Ok(new { profile = (object?)null, message = "No profile found." });

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

                return Ok(new { profile = dto, type = "Tenant" });
            }

            return Ok(new { profile = (object?)null, message = "No profile found." });
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> Update([FromBody] UpdateProfileDto update)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdStr, out var userId)) return Unauthorized();

            var userableType = User.FindFirstValue("userable_type");
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return Unauthorized();

            if (userableType == "Landlord")
            {
                var landlord = await _landlordRepository.GetByUserIdAsync(userId);
                if (landlord == null) return BadRequest("Landlord profile not found.");

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

                return Ok(new { profile = dto, type = "Landlord" });
            }
            else if (userableType == "Tenant")
            {
                var userableIdStr = User.FindFirstValue("userable_id");
                if (!long.TryParse(userableIdStr, out var tenantId))
                    return BadRequest("Tenant profile not found.");

                var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                if (tenant == null) return BadRequest("Tenant profile not found.");

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

                return Ok(new { profile = dto, type = "Tenant" });
            }

            return BadRequest("Unknown user type.");
        }
    }
}
