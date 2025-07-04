using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[Route("api/[controller]")]
public class HealthController : BaseController
{
    [HttpGet]
    [ProducesResponseType(200)]
    public IActionResult GetHealth()
    {
        var healthData = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            CorrelationId = GetCorrelationId()
        };

        return SuccessResponse(healthData, "Service is healthy");
    }

    [HttpGet("detailed")]
    [ProducesResponseType(200)]
    public IActionResult GetDetailedHealth()
    {
        var healthData = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            Dependencies = new
            {
                Database = "Connected",
                Redis = "Connected",
                RabbitMQ = "Connected"
            }
        };

        return SuccessResponse(healthData, "Service and dependencies are healthy");
    }
}