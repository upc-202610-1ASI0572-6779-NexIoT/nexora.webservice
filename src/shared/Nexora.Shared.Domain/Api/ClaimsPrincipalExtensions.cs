using System.Security.Claims;

namespace Nexora.Shared.Domain.Api
{
    public static class ClaimsPrincipalExtensions
    {
        public static long? GetUserId(this ClaimsPrincipal principal)
        {
            var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(userIdString, out var userId))
                return userId;
            return null;
        }
    }
}
