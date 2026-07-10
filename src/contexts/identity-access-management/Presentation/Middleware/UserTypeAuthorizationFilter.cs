using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Nexora.Shared.Domain.Api;
using Nexora.Shared.Domain.Resources;
using System.Security.Claims;

namespace Nexora.WebApi.Middleware
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireUserTypeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _requiredType;

        public RequireUserTypeAttribute(string requiredType)
        {
            _requiredType = requiredType;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var userableType = context.HttpContext.User.FindFirstValue("userable_type");

            if (string.IsNullOrEmpty(userableType) || userableType != _requiredType)
            {
                var localizer = context.HttpContext.RequestServices
                    .GetRequiredService<IStringLocalizer<SharedMessages>>();

                var message = _requiredType == "Landlord"
                    ? localizer["Forbidden_LandlordOnly"]
                    : localizer["Forbidden_TenantOnly"];

                context.Result = new ObjectResult(new ErrorResponse("Forbidden", message))
                {
                    StatusCode = 403
                };
            }
        }
    }
}
