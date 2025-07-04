using ECommerce.Core.DTOs;
using ECommerce.Core.DTOs.Common;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected string GetCorrelationId()
    {
        return HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();
    }

    protected string? GetCurrentUserId()
    {
        return User?.Identity?.Name ?? User?.FindFirst("sub")?.Value;
    }

    protected IActionResult SuccessResponse<T>(T data, string? message = null)
    {
        var response = ApiResponse<T>.SuccessResponse(data, message);
        response.CorrelationId = GetCorrelationId();
        return Ok(response);
    }

    protected IActionResult CreatedResponse<T>(T data, string actionName, object? routeValues = null, string? message = null)
    {
        var response = ApiResponse<T>.SuccessResponse(data, message);
        response.CorrelationId = GetCorrelationId();
        return CreatedAtAction(actionName, routeValues, response);
    }

    protected IActionResult ErrorResponse<T>(string message, List<string>? errors = null, int statusCode = 400)
    {
        var response = ApiResponse<T>.ErrorResponse(message, errors);
        response.CorrelationId = GetCorrelationId();

        return statusCode switch
        {
            400 => BadRequest(response),
            401 => Unauthorized(response),
            403 => StatusCode(403, response),
            404 => NotFound(response),
            409 => Conflict(response),
            422 => UnprocessableEntity(response),
            _ => StatusCode(statusCode, response)
        };
    }

    protected IActionResult ValidationErrorResponse<T>(List<string> errors)
    {
        return ErrorResponse<T>("Validation failed", errors, 400);
    }

    protected IActionResult NotFoundResponse<T>(string resource, string identifier)
    {
        var message = $"{resource} with identifier '{identifier}' was not found";
        return ErrorResponse<T>(message, null, 404);
    }
    protected IActionResult HandleServiceResult<T>(ApiResponse<T> result, string? successMessage = null)
    {
        if (result.Success)
        {
            if (!string.IsNullOrEmpty(successMessage))
                result.Message = successMessage;

            result.CorrelationId = GetCorrelationId();
            return Ok(result);
        }

        result.CorrelationId = GetCorrelationId();
        return BadRequest(result);
    }

    protected IActionResult HandleServiceResultForCreation<T>(
        ApiResponse<T> result,
        string actionName,
        object? routeValues = null,
        string? successMessage = null)
    {
        if (result.Success)
        {
            if (!string.IsNullOrEmpty(successMessage))
                result.Message = successMessage;

            result.CorrelationId = GetCorrelationId();
            return CreatedAtAction(actionName, routeValues, result);
        }

        result.CorrelationId = GetCorrelationId();
        return BadRequest(result);
    }

    protected bool ValidateParameter(string parameterName, string? parameterValue)
    {
        if (string.IsNullOrWhiteSpace(parameterValue))
        {
            ModelState.AddModelError(parameterName, $"{parameterName} is required");
            return false;
        }
        return true;
    }

    protected bool ValidateGuidParameter(string parameterName, string? parameterValue)
    {
        if (string.IsNullOrWhiteSpace(parameterValue))
        {
            ModelState.AddModelError(parameterName, $"{parameterName} is required");
            return false;
        }

        if (!Guid.TryParse(parameterValue, out _))
        {
            ModelState.AddModelError(parameterName, $"{parameterName} must be a valid GUID");
            return false;
        }

        return true;
    }

    protected IActionResult? ValidateModelState<T>()
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .SelectMany(x => x.Value!.Errors)
                .Select(x => x.ErrorMessage)
                .ToList();

            return ValidationErrorResponse<T>(errors);
        }

        return null;
    }

    protected void LogInformation(ILogger logger, string message, params object[] args)
    {
        var correlationId = GetCorrelationId();
        var argsWithCorrelation = args.Concat(new object[] { correlationId }).ToArray();
        logger.LogInformation(message + " CorrelationId: {CorrelationId}", argsWithCorrelation);
    }

    protected void LogError(ILogger logger, Exception exception, string message, params object[] args)
    {
        var correlationId = GetCorrelationId();
        var argsWithCorrelation = args.Concat(new object[] { correlationId }).ToArray();
        logger.LogError(exception, message + " CorrelationId: {CorrelationId}", argsWithCorrelation);
    }
}