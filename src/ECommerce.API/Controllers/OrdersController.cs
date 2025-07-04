using ECommerce.Application.Services;
using ECommerce.Application.Services.Interface;
using ECommerce.Core.DTOs;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Request;
using ECommerce.Core.DTOs.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[Route("api/[controller]")]
[Authorize]
public class OrdersController : BaseController
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), 400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var modelValidation = ValidateModelState<OrderResponse>();
        if (modelValidation != null)
            return modelValidation;

        LogInformation(_logger, "Creating order request received for user {UserId}", request.UserId);

        var correlationId = GetCorrelationId();
        var result = await _orderService.CreateOrderAsync(request, correlationId);

        return HandleServiceResultForCreation(
            result,
            nameof(GetOrdersByUserId),
            new { userId = request.UserId },
            "Order created successfully");
    }

    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(ApiResponse<List<OrderResponse>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<List<OrderResponse>>), 400)]
    [ProducesResponseType(typeof(ApiResponse<List<OrderResponse>>), 404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetOrdersByUserId(string userId)
    {
        if (!ValidateParameter(nameof(userId), userId))
        {
            return ValidationErrorResponse<List<OrderResponse>>(new List<string> { "User ID is required" });
        }

        LogInformation(_logger, "Get orders request received for user {UserId}", userId);

        var correlationId = GetCorrelationId();
        var result = await _orderService.GetOrdersByUserIdAsync(userId, correlationId);

        return HandleServiceResult(result);
    }

    [HttpGet("order/{orderId}")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), 400)]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), 404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetOrderById(string orderId)
    {
        if (!ValidateGuidParameter(nameof(orderId), orderId))
        {
            return ValidationErrorResponse<OrderResponse>(
                ModelState.SelectMany(x => x.Value!.Errors).Select(x => x.ErrorMessage).ToList());
        }

        LogInformation(_logger, "Get order request received for order {OrderId}", orderId);

        var correlationId = GetCorrelationId();
        var result = await _orderService.GetOrderByIdAsync(orderId, correlationId);

        return HandleServiceResult(result);
    }
}