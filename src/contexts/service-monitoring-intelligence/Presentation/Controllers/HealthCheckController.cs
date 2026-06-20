using Microsoft.AspNetCore.Mvc;
using Nexora.Application.Services;

namespace Nexora.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/health-checks")]
    public class HealthCheckController : ControllerBase
    {
        private readonly CheckSystemHealthUseCase _healthUseCase;

        public HealthCheckController(CheckSystemHealthUseCase healthUseCase)
        {
            _healthUseCase = healthUseCase;
        }

        [HttpGet]
        public IActionResult GetStatus()
        {
            var status = _healthUseCase.Execute();
            return Ok(status);
        }
    }
}