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
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ProfileController(ILandlordRepository landlordRepository, IUserRepository userRepository, IUnitOfWork unitOfWork)
        {
            _landlordRepository = landlordRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrent()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdStr, out var userId)) return Unauthorized();

            var landlord = await _landlordRepository.GetByUserIdAsync(userId);
            var user = await _userRepository.GetByIdAsync(userId);

            if (landlord == null || user == null)
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

            return Ok(new { profile = dto });
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> Update([FromBody] UpdateProfileDto update)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdStr, out var userId)) return Unauthorized();

            var landlord = await _landlordRepository.GetByUserIdAsync(userId);
            if (landlord == null) return BadRequest("Landlord profile not found.");

            landlord.UpdatePersonalInfo(update.FirstName, update.LastName, update.Country, update.City, update.Address, update.PhoneNumber);

            await _landlordRepository.UpdateAsync(landlord);
            await _unitOfWork.SaveChangesAsync();

            var user = await _userRepository.GetByIdAsync(userId);

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

            return Ok(new { profile = dto });
        }
    }
}
