using System.Diagnostics;

namespace ECommerce.API.Middleware;

public class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public PerformanceMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.Items["CorrelationId"]?.ToString();

        if (ShouldSkipMonitoring(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var requestStartTime = DateTime.UtcNow;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var elapsed = stopwatch.ElapsedMilliseconds;
            var statusCode = context.Response.StatusCode;

            context.Response.Headers.Add("X-Response-Time", $"{elapsed}ms");
            context.Response.Headers.Add("X-Request-Start-Time", requestStartTime.ToString("O"));

            LogPerformanceMetrics(context, elapsed, correlationId);

            var slowRequestThreshold = _configuration.GetValue<int>("Monitoring:SlowRequestThresholdMs", 5000);
            if (elapsed > slowRequestThreshold)
            {
                LogSlowRequest(context, elapsed, correlationId);
            }

            if (statusCode >= 400)
            {
                LogErrorResponse(context, elapsed, correlationId, statusCode);
            }
        }
    }

    private static bool ShouldSkipMonitoring(PathString path)
    {
        var pathsToSkip = new[]
        {
            "/health",
            "/metrics",
            "/favicon.ico",
            "/swagger",
            "/_framework",
            "/css",
            "/js",
            "/images"
        };

        return pathsToSkip.Any(skipPath =>
            path.StartsWithSegments(skipPath, StringComparison.OrdinalIgnoreCase));
    }

    private void LogPerformanceMetrics(HttpContext context, long elapsed, string? correlationId)
    {
        _logger.LogDebug(
            "Request completed: {Method} {Path} => {StatusCode} in {ElapsedMs}ms. " +
            "CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            elapsed,
            correlationId);
    }

    private void LogSlowRequest(HttpContext context, long elapsed, string? correlationId)
    {
        var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault();
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        _logger.LogWarning(
            "Slow request detected: {Method} {Path} took {ElapsedMs}ms. " +
            "StatusCode: {StatusCode}, RemoteIP: {RemoteIP}, UserAgent: {UserAgent}, " +
            "CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            elapsed,
            context.Response.StatusCode,
            remoteIp,
            userAgent,
            correlationId);
    }

    private void LogErrorResponse(HttpContext context, long elapsed, string? correlationId, int statusCode)
    {
        var logLevel = statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel,
            "Request completed with error: {Method} {Path} => {StatusCode} in {ElapsedMs}ms. " +
            "CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            statusCode,
            elapsed,
            correlationId);
    }
}