using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nexora.Application.Dto;
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
                context.Result = new ObjectResult(
                    new ErrorResponseDto(
                        "Forbidden",
                        _requiredType == "Landlord"
                            ? "Acceso denegado. Esta plataforma es exclusiva para arrendadores."
                            : "Acceso denegado. Esta plataforma es exclusiva para arrendatarios."
                    )
                )
                {
                    StatusCode = 403
                };
            }
        }
    }
}