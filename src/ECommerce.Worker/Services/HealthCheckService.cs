using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ECommerce.Worker.Services;

public class HealthCheckService : IHealthCheck
{
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(ILogger<HealthCheckService> logger)
    {
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(100, cancellationToken);

            _logger.LogDebug("Health check passed");

            return HealthCheckResult.Healthy("Worker is running smoothly");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy("Worker health check failed", ex);
        }
    }
}