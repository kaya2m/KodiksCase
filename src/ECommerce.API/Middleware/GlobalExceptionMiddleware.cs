using System.Net;
using System.Text.Json;
using ECommerce.Application.Exceptions;
using ECommerce.Core.DTOs;

namespace ECommerce.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        _logger.LogError(exception,
            "Unhandled exception occurred. Method: {Method}, Path: {Path}, CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        var response = context.Response;
        response.ContentType = "application/json";

        var (statusCode, message, errors) = GetErrorResponse(exception);
        response.StatusCode = statusCode;

        var apiResponse = new
        {
            Success = false,
            Message = message,
            Errors = errors,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path.ToString(),
            Method = context.Request.Method,
            Details = _environment.IsDevelopment() ? exception.ToString() : null
        };

        var jsonResponse = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        });

        await response.WriteAsync(jsonResponse);
    }

    private (int statusCode, string message, List<string>? errors) GetErrorResponse(Exception exception)
    {
        return exception switch
        {
            BusinessException businessEx => (
                StatusCodes.Status400BadRequest,
                businessEx.Message,
                null
            ),
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                validationEx.Errors?.SelectMany(x => x.Value).ToList()
            ),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized access. Please check your credentials.",
                null
            ),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "The requested resource was not found.",
                null
            ),
            TimeoutException => (
                StatusCodes.Status408RequestTimeout,
                "The request timed out. Please try again later.",
                null
            ),
            NotSupportedException => (
                StatusCodes.Status501NotImplemented,
                "The requested operation is not supported.",
                null
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                _environment.IsDevelopment()
                    ? exception.Message
                    : "An internal server error occurred. Please try again later.",
                null
            )
        };
    }
}