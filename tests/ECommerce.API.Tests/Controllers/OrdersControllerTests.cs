using ECommerce.API.Controllers;
using ECommerce.Application.Services.Interface;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Request;
using ECommerce.Core.DTOs.Response;
using ECommerce.Core.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECommerce.API.Tests.Controllers;

public class OrdersControllerTests
{
    private readonly Mock<IOrderService> _mockOrderService;
    private readonly Mock<ILogger<OrdersController>> _mockLogger;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        _mockOrderService = new Mock<IOrderService>();
        _mockLogger = new Mock<ILogger<OrdersController>>();
        _controller = new OrdersController(_mockOrderService.Object, _mockLogger.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Items["CorrelationId"] = "test-correlation-id";
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_ReturnsCreatedResult()
    {
        var request = new CreateOrderRequest
        {
            UserId = "user123",
            ProductId = "product001",
            Quantity = 2,
            PaymentMethod = PaymentMethod.CreditCard
        };

        var expectedResponse = new OrderResponse
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            PaymentMethod = request.PaymentMethod,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var serviceResponse = ApiResponse<OrderResponse>.SuccessResponse(expectedResponse, "Order created");
        _mockOrderService.Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequest>(), It.IsAny<string>()))
            .ReturnsAsync(serviceResponse);

        var result = await _controller.CreateOrder(request);

        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult?.Value.Should().BeOfType<ApiResponse<OrderResponse>>();

        var apiResponse = createdResult?.Value as ApiResponse<OrderResponse>;
        apiResponse?.Success.Should().BeTrue();
        apiResponse?.Data?.UserId.Should().Be(request.UserId);

        _mockOrderService.Verify(x => x.CreateOrderAsync(request, "test-correlation-id"), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidRequest_ReturnsBadRequest()
    {
        var request = new CreateOrderRequest
        {
            UserId = "", 
            ProductId = "product001",
            Quantity = 2,
            PaymentMethod = PaymentMethod.CreditCard
        };

        _controller.ModelState.AddModelError("UserId", "User ID is required");

        var result = await _controller.CreateOrder(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetOrdersByUserId_WithValidUserId_ReturnsOkResult()
    {
        var userId = "user123";
        var orders = new List<OrderResponse>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProductId = "product001",
                Quantity = 1,
                PaymentMethod = PaymentMethod.CreditCard,
                Status = OrderStatus.Completed
            }
        };

        var serviceResponse = ApiResponse<List<OrderResponse>>.SuccessResponse(orders);
        _mockOrderService.Setup(x => x.GetOrdersByUserIdAsync(userId, It.IsAny<string>()))
            .ReturnsAsync(serviceResponse);

        var result = await _controller.GetOrdersByUserId(userId);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var apiResponse = okResult?.Value as ApiResponse<List<OrderResponse>>;

        apiResponse?.Success.Should().BeTrue();
        apiResponse?.Data.Should().HaveCount(1);
        apiResponse?.Data?.First().UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetOrdersByUserId_WithEmptyUserId_ReturnsBadRequest()
    {
        var userId = "";

        var result = await _controller.GetOrdersByUserId(userId);

        result.Should().BeOfType<BadRequestObjectResult>();
        _mockOrderService.Verify(x => x.GetOrdersByUserIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOrderById_WithValidId_ReturnsOkResult()
    {
        var orderId = Guid.NewGuid().ToString();
        var order = new OrderResponse
        {
            Id = Guid.Parse(orderId),
            UserId = "user123",
            ProductId = "product001",
            Quantity = 1,
            PaymentMethod = PaymentMethod.CreditCard,
            Status = OrderStatus.Completed
        };

        var serviceResponse = ApiResponse<OrderResponse>.SuccessResponse(order);
        _mockOrderService.Setup(x => x.GetOrderByIdAsync(orderId, It.IsAny<string>()))
            .ReturnsAsync(serviceResponse);

        var result = await _controller.GetOrderById(orderId);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var apiResponse = okResult?.Value as ApiResponse<OrderResponse>;

        apiResponse?.Success.Should().BeTrue();
        apiResponse?.Data?.Id.Should().Be(Guid.Parse(orderId));
    }

    [Fact]
    public async Task GetOrderById_WithInvalidGuid_ReturnsBadRequest()
    {
        var invalidOrderId = "invalid-guid";

        var result = await _controller.GetOrderById(invalidOrderId);

        result.Should().BeOfType<BadRequestObjectResult>();
        _mockOrderService.Verify(x => x.GetOrderByIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}