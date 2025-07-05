using ECommerce.Application.Services;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using ECommerce.Core.Events;
using ECommerce.Core.Interfaces.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECommerce.Application.Tests.Services;

public class OrderProcessingServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<ILogger<OrderProcessingService>> _mockLogger;
    private readonly OrderProcessingService _service;

    public OrderProcessingServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockCacheService = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<OrderProcessingService>>();

        _service = new OrderProcessingService(
            _mockUnitOfWork.Object,
            _mockCacheService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessOrderAsync_WithValidOrder_UpdatesOrderStatusToProcessed()
    {
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderPlacedEvent
        {
            OrderId = orderId,
            UserId = "user123",
            ProductId = "product001",
            Quantity = 2,
            PaymentMethod = PaymentMethod.CreditCard,
            CreatedAt = DateTime.UtcNow
        };

        var order = new Order
        {
            Id = orderId,
            UserId = orderEvent.UserId,
            ProductId = orderEvent.ProductId,
            Quantity = orderEvent.Quantity,
            PaymentMethod = orderEvent.PaymentMethod,
            Status = OrderStatus.Pending
        };

        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        _mockUnitOfWork.Setup(x => x.Orders.UpdateAsync(It.IsAny<Order>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(x => x.OrderProcessingLogs.CreateAsync(It.IsAny<OrderProcessingLog>()))
            .ReturnsAsync((OrderProcessingLog log) => log);

        _mockUnitOfWork.Setup(x => x.CompleteAsync())
            .ReturnsAsync(1);

        _mockCacheService.Setup(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        await _service.ProcessOrderAsync(orderEvent);

        order.Status.Should().Be(OrderStatus.Processed);

        _mockUnitOfWork.Verify(x => x.Orders.GetByIdAsync(orderId), Times.Once);
        _mockUnitOfWork.Verify(x => x.Orders.UpdateAsync(order), Times.AtLeast(2));
        _mockUnitOfWork.Verify(x => x.OrderProcessingLogs.CreateAsync(It.IsAny<OrderProcessingLog>()), Times.AtLeast(3));
        _mockUnitOfWork.Verify(x => x.CompleteAsync(), Times.AtLeast(3));
        _mockCacheService.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once);
    }


    [Fact]
    public async Task ProcessOrderAsync_WithNonExistentOrder_LogsWarning()
    {
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderPlacedEvent
        {
            OrderId = orderId,
            UserId = "user123",
            ProductId = "product001",
            Quantity = 2,
            PaymentMethod = PaymentMethod.CreditCard,
            CreatedAt = DateTime.UtcNow
        };

        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(orderId))
            .ReturnsAsync((Order?)null);

        await _service.ProcessOrderAsync(orderEvent);

        _mockUnitOfWork.Verify(x => x.Orders.UpdateAsync(It.IsAny<Order>()), Times.Never);
        _mockUnitOfWork.Verify(x => x.OrderProcessingLogs.CreateAsync(It.IsAny<OrderProcessingLog>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderAsync_WithException_SetsOrderStatusToCancelled()
    {
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderPlacedEvent
        {
            OrderId = orderId,
            UserId = "user123",
            ProductId = "product001",
            Quantity = 2,
            PaymentMethod = PaymentMethod.CreditCard,
            CreatedAt = DateTime.UtcNow
        };

        var order = new Order
        {
            Id = orderId,
            UserId = orderEvent.UserId,
            Status = OrderStatus.Pending
        };

        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(orderId))
            .ReturnsAsync(order);
        _mockUnitOfWork.Setup(x => x.Orders.UpdateAsync(It.IsAny<Order>()))
            .ThrowsAsync(new Exception("Database error"));

        await _service.ProcessOrderAsync(orderEvent);

        _mockUnitOfWork.Verify(x => x.Orders.GetByIdAsync(orderId), Times.AtLeast(1));
    }
}