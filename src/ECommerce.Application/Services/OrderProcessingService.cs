using ECommerce.Application.Services.Interface;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using ECommerce.Core.Events;
using ECommerce.Core.Interfaces.Common;
using Microsoft.Extensions.Logging;
namespace ECommerce.Application.Services;

public class OrderProcessingService : IOrderProcessingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<OrderProcessingService> logger)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task ProcessOrderAsync(OrderPlacedEvent orderEvent)
    {
        try
        {
            _logger.LogInformation("Processing order {OrderId} for user {UserId}",
                orderEvent.OrderId, orderEvent.UserId);

            var order = await _unitOfWork.Orders.GetByIdAsync(orderEvent.OrderId);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for processing", orderEvent.OrderId);
                return;
            }

            order.Status = OrderStatus.Processing;
             await  _unitOfWork.Orders.UpdateAsync(order);

            var processingLog = new OrderProcessingLog
            {
                OrderId = order.Id,
                Status = "Processing",
                Message = "Order processing started"
            };
            await _unitOfWork.OrderProcessingLogs.CreateAsync(processingLog);

            await _unitOfWork.CompleteAsync();

            await Task.Delay(TimeSpan.FromSeconds(5));

            var redisKey = $"order_processed_{order.Id}";
            var redisValue = new
            {
                OrderId = order.Id,
                ProcessedAt = DateTime.UtcNow,
                Status = "Processed"
            };

            await _cacheService.SetAsync(redisKey, redisValue, TimeSpan.FromHours(24));

            order.Status = OrderStatus.Processed;
             await  _unitOfWork.Orders.UpdateAsync(order);

            var completionLog = new OrderProcessingLog
            {
                OrderId = order.Id,
                Status = "Processed",
                Message = $"Order processed successfully at {DateTime.UtcNow}"
            };
            await _unitOfWork.OrderProcessingLogs.CreateAsync(completionLog);

            await _unitOfWork.CompleteAsync();

            await SendNotificationAsync(order);

            _logger.LogInformation("Order {OrderId} processed successfully for user {UserId}",
                order.Id, order.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId}", orderEvent.OrderId);

            try
            {
                var order = await _unitOfWork.Orders.GetByIdAsync(orderEvent.OrderId);
                if (order != null)
                {
                    order.Status = OrderStatus.Cancelled;
                    await _unitOfWork.Orders.UpdateAsync(order);

                    var errorLog = new OrderProcessingLog
                    {
                        OrderId = order.Id,
                        Status = "Error",
                        Message = $"Order processing failed: {ex.Message}"
                    };
                    await _unitOfWork.OrderProcessingLogs.CreateAsync(errorLog);

                    await _unitOfWork.CompleteAsync();
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Error updating order status after processing failure for order {OrderId}",
                    orderEvent.OrderId);
            }
        }
    }

    private async Task SendNotificationAsync(Order order)
    {
        try
        {
            _logger.LogInformation("Sending notification for order {OrderId} to user {UserId}",
                order.Id, order.UserId);

            var notificationMessage = $"Your order {order.Id} has been processed successfully!";

            var notificationLog = new OrderProcessingLog
            {
                OrderId = order.Id,
                Status = "Notification",
                Message = $"Notification sent: {notificationMessage}"
            };

            await _unitOfWork.OrderProcessingLogs.CreateAsync(notificationLog);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Notification sent successfully for order {OrderId}", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification for order {OrderId}", order.Id);
        }
    }
}
