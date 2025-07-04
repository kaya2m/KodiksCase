using ECommerce.Core.Interfaces.Common;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Services;
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
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetDetailedHealth()
    {
        try
        {
            var healthChecks = await CheckDependenciesAsync();
            var overallStatus = healthChecks.All(x => x.Value.IsHealthy) ? "Healthy" : "Unhealthy";

            var healthData = new
            {
                Status = overallStatus,
                Timestamp = DateTime.UtcNow,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                ApplicationName = "ECommerce API",
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                UpTime = GetUpTime(),
                Dependencies = healthChecks.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        Status = kvp.Value.IsHealthy ? "Connected" : "Disconnected",
                        ResponseTime = kvp.Value.ResponseTime,
                        LastChecked = kvp.Value.LastChecked,
                        ErrorMessage = kvp.Value.ErrorMessage
                    }
                ),
                SystemInfo = new
                {
                    Platform = Environment.OSVersion.Platform.ToString(),
                    OSVersion = Environment.OSVersion.VersionString,
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = GC.GetTotalMemory(false),
                    GCGeneration = new
                    {
                        Gen0 = GC.CollectionCount(0),
                        Gen1 = GC.CollectionCount(1),
                        Gen2 = GC.CollectionCount(2)
                    }
                }
            };

            var statusCode = overallStatus == "Healthy" ? 200 : 503;
            return StatusCode(statusCode, SuccessResponse(healthData, $"Service is {overallStatus.ToLower()}"));
        }
        catch (Exception ex)
        {
            var errorData = new
            {
                Status = "Error",
                Timestamp = DateTime.UtcNow,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                ErrorMessage = "Health check failed",
                Dependencies = new
                {
                    Database = "Unknown",
                    Redis = "Unknown",
                    RabbitMQ = "Unknown"
                }
            };

            return StatusCode(500, ErrorResponse<object>("Health check failed", new List<string> { ex.Message }, 500));
        }
    }

    private async Task<Dictionary<string, HealthCheckResult>> CheckDependenciesAsync()
    {
        var results = new Dictionary<string, HealthCheckResult>();

        results["Database"] = await CheckDatabaseAsync();

        results["Redis"] = await CheckRedisAsync();

        results["RabbitMQ"] = await CheckRabbitMQAsync();

        return results;
    }

    private async Task<HealthCheckResult> CheckDatabaseAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

            await dbContext.Database.CanConnectAsync();
            stopwatch.Stop();

            return new HealthCheckResult
            {
                IsHealthy = true,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new HealthCheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                LastChecked = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<HealthCheckResult> CheckRedisAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

            var testKey = $"health_check_{Guid.NewGuid()}";
            var testValue = new { Test = "HealthCheck", Timestamp = DateTime.UtcNow };

            await cacheService.SetAsync(testKey, testValue, TimeSpan.FromSeconds(10));
            var result = await cacheService.GetAsync<object>(testKey);
            await cacheService.RemoveAsync(testKey);

            stopwatch.Stop();

            return new HealthCheckResult
            {
                IsHealthy = result != null,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new HealthCheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                LastChecked = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<HealthCheckResult> CheckRabbitMQAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

            var isHealthy = true;
            if (messagePublisher is RabbitMQPublisher rabbitMQPublisher)
            {
                isHealthy = rabbitMQPublisher.IsHealthy();
            }

            stopwatch.Stop();

            return new HealthCheckResult
            {
                IsHealthy = isHealthy,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new HealthCheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                LastChecked = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private string GetUpTime()
    {
        try
        {
            var startTime = System.Diagnostics.Process.GetCurrentProcess().StartTime;
            var upTime = DateTime.Now - startTime;
            return $"{upTime.Days}d {upTime.Hours}h {upTime.Minutes}m {upTime.Seconds}s";
        }
        catch
        {
            return "Unknown";
        }
    }
    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public long ResponseTime { get; set; }
        public DateTime LastChecked { get; set; }
        public string? ErrorMessage { get; set; }
    }
}