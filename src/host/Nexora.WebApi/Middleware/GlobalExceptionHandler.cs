using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nexora.Shared.Domain.Api;

namespace Nexora.WebApi.Middleware
{
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Unhandled exception: {ExceptionType}", exception.GetType().Name);

            var (statusCode, code, message) = exception switch
            {
                NotFoundException notFoundEx =>
                    (StatusCodes.Status404NotFound, "NotFound", notFoundEx.Message),

                ValidationException validationEx =>
                    (StatusCodes.Status400BadRequest, "ValidationFailed", validationEx.Message),

                ConflictException conflictEx =>
                    (StatusCodes.Status409Conflict, "Conflict", conflictEx.Message),

                ForbiddenException forbiddenEx =>
                    (StatusCodes.Status403Forbidden, "Forbidden", forbiddenEx.Message),

                UnauthorizedAccessException =>
                    (StatusCodes.Status401Unauthorized, "Unauthorized",
                     "Authentication required. Please provide a valid token."),

                _ =>
                    (StatusCodes.Status500InternalServerError, "InternalServerError",
                     "An unexpected error occurred. Please try again later.")
            };

            httpContext.Response.StatusCode = statusCode;

            if (exception is ValidationException validationException)
            {
                var validationResponse = new ValidationErrorResponse(
                    code,
                    message,
                    validationException.Errors);

                await httpContext.Response.WriteAsJsonAsync(validationResponse, cancellationToken);
            }
            else
            {
                var errorResponse = new ErrorResponse(code, message);
                await httpContext.Response.WriteAsJsonAsync(errorResponse, cancellationToken);
            }

            return true;
        }
    }
}
