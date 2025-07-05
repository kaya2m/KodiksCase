using ECommerce.Application.Services;
using ECommerce.Core.DTOs.Request;
using ECommerce.Core.DTOs.Response;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using ECommerce.Core.Events;
using ECommerce.Core.Interfaces.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECommerce.Application.Tests.Services;

public class OrderServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IMessagePublisher> _mockMessagePublisher;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<ILogger<OrderService>> _mockLogger;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMessagePublisher = new Mock<IMessagePublisher>();
        _mockCacheService = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<OrderService>>();

        _orderService = new OrderService(
            _mockUnitOfWork.Object,
            _mockMessagePublisher.Object,
            _mockCacheService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_WithValidRequest_ReturnsSuccessResponse()
    {
        var request = new CreateOrderRequest
        {
            UserId = "user123",
            ProductId = "product001",
            Quantity = 2,
            PaymentMethod = PaymentMethod.CreditCard
        };

        var createdOrder = new Order
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            PaymentMethod = request.PaymentMethod,
            Status = OrderStatus.Pending
        };

        _mockUnitOfWork.Setup(x => x.Orders.CreateAsync(It.IsAny<Order>()))
            .ReturnsAsync(createdOrder);
        _mockUnitOfWork.Setup(x => x.CompleteAsync())
            .ReturnsAsync(1);

        var result = await _orderService.CreateOrderAsync(request, "test-correlation");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.UserId.Should().Be(request.UserId);
        result.Data.ProductId.Should().Be(request.ProductId);
        result.Data.Status.Should().Be(OrderStatus.Pending);

        _mockUnitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
        _mockUnitOfWork.Verify(x => x.Orders.CreateAsync(It.IsAny<Order>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.CompleteAsync(), Times.Once);
        _mockUnitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
        _mockMessagePublisher.Verify(x => x.PublishAsync(It.IsAny<OrderPlacedEvent>(), It.IsAny<string>()), Times.Once);
        _mockCacheService.Verify(x => x.RemoveAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_WithInvalidUserId_ReturnsErrorResponse()
    {
        var request = new CreateOrderRequest
        {
            UserId = "", // Invalid
            ProductId = "product001",
            Quantity = 2,
            PaymentMethod = PaymentMethod.CreditCard
        };

        var result = await _orderService.CreateOrderAsync(request, "test-correlation");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("User ID is required");

        _mockUnitOfWork.Verify(x => x.Orders.CreateAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_WithZeroQuantity_ReturnsErrorResponse()
    {
        var request = new CreateOrderRequest
        {
            UserId = "user123",
            ProductId = "product001",
            Quantity = 0, 
            PaymentMethod = PaymentMethod.CreditCard
        };

        var result = await _orderService.CreateOrderAsync(request, "test-correlation");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Quantity must be greater than 0");
    }

    [Fact]
    public async Task GetOrdersByUserIdAsync_WithCachedData_ReturnsCachedResponse()
    {
        var userId = "user123";
        var cachedOrders = new List<OrderResponse>
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

        _mockCacheService.Setup(x => x.GetAsync<List<OrderResponse>>(It.IsAny<string>()))
            .ReturnsAsync(cachedOrders);

        var result = await _orderService.GetOrdersByUserIdAsync(userId, "test-correlation");

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data!.First().UserId.Should().Be(userId);

        _mockUnitOfWork.Verify(x => x.Orders.GetByUserIdAsync(It.IsAny<string>()), Times.Never);
        _mockCacheService.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task GetOrdersByUserIdAsync_WithoutCachedData_QueriesDatabase()
    {
        var userId = "user123";
        var dbOrders = new List<Order>
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

        _mockCacheService.Setup(x => x.GetAsync<List<OrderResponse>>(It.IsAny<string>()))
            .ReturnsAsync((List<OrderResponse>?)null);
        _mockUnitOfWork.Setup(x => x.Orders.GetByUserIdAsync(userId))
            .ReturnsAsync(dbOrders);

        var result = await _orderService.GetOrdersByUserIdAsync(userId, "test-correlation");

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data!.First().UserId.Should().Be(userId);

        _mockUnitOfWork.Verify(x => x.Orders.GetByUserIdAsync(userId), Times.Once);
        _mockCacheService.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    [Theory]
    [InlineData(PaymentMethod.CreditCard)]
    [InlineData(PaymentMethod.BankTransfer)]
    public async Task CreateOrderAsync_WithDifferentPaymentMethods_ReturnsSuccessResponse(PaymentMethod paymentMethod)
    {
        var request = new CreateOrderRequest
        {
            UserId = "user123",
            ProductId = "product001",
            Quantity = 1,
            PaymentMethod = paymentMethod
        };

        var createdOrder = new Order
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            PaymentMethod = request.PaymentMethod,
            Status = OrderStatus.Pending
        };

        _mockUnitOfWork.Setup(x => x.Orders.CreateAsync(It.IsAny<Order>()))
            .ReturnsAsync(createdOrder);
        _mockUnitOfWork.Setup(x => x.CompleteAsync())
            .ReturnsAsync(1);

        var result = await _orderService.CreateOrderAsync(request, "test-correlation");

        result.Success.Should().BeTrue();
        result.Data!.PaymentMethod.Should().Be(paymentMethod);
    }
}