using ECommerce.Application.Services.Interface;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Request;
using ECommerce.Core.DTOs.Response;
using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using ECommerce.Core.Events;
using ECommerce.Core.Interfaces.Common;
using ECommerce.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMessagePublisher _messagePublisher;
        private readonly ICacheService _cacheService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IUnitOfWork unitOfWork,
            IMessagePublisher messagePublisher,
            ICacheService cacheService,
            ILogger<OrderService> logger)
        {
            _unitOfWork = unitOfWork;
            _messagePublisher = messagePublisher;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<ApiResponse<OrderResponse>> CreateOrderAsync(CreateOrderRequest request, string correlationId)
        {
            try
            {
                _logger.LogInformation("Creating order for user {UserId}. CorrelationId: {CorrelationId}",
                    request.UserId, correlationId);

                var validationResult = ValidateOrderRequest(request);
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                var order = new Order
                {
                    UserId = request.UserId,
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    PaymentMethod = request.PaymentMethod,
                    Status = OrderStatus.Pending
                };

                await _unitOfWork.BeginTransactionAsync();

                try
                {
                    await _unitOfWork.Orders.CreateAsync(order);
                    await _unitOfWork.CompleteAsync();

                    var orderEvent = new OrderPlacedEvent
                    {
                        OrderId = order.Id,
                        UserId = order.UserId,
                        ProductId = order.ProductId,
                        Quantity = order.Quantity,
                        PaymentMethod = order.PaymentMethod,
                        CreatedAt = order.CreatedAt
                    };

                    await _messagePublisher.PublishAsync(orderEvent, QueueNames.ORDER_PLACED);

                    var cacheKey = string.Format(CacheKeys.USER_ORDERS, request.UserId);
                    await _cacheService.RemoveAsync(cacheKey);

                    await _unitOfWork.CommitTransactionAsync();

                    _logger.LogInformation("Order created successfully. OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                        order.Id, correlationId);

                    var response = MapToOrderResponse(order);
                    return ApiResponse<OrderResponse>.SuccessResponse(response, "Order created successfully");
                }
                catch (Exception ex)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for user {UserId}. CorrelationId: {CorrelationId}",
                    request.UserId, correlationId);

                return ApiResponse<OrderResponse>.ErrorResponse(
                    "An error occurred while creating the order. Please try again later.");
            }
        }

        public async Task<ApiResponse<List<OrderResponse>>> GetOrdersByUserIdAsync(string userId, string correlationId)
        {
            try
            {
                _logger.LogInformation("Retrieving orders for user {UserId}. CorrelationId: {CorrelationId}",
                    userId, correlationId);

                var cacheKey = string.Format(CacheKeys.USER_ORDERS, userId);
                var cachedOrders = await _cacheService.GetAsync<List<OrderResponse>>(cacheKey);

                if (cachedOrders != null)
                {
                    _logger.LogDebug("Orders retrieved from cache for user {UserId}. CorrelationId: {CorrelationId}",
                        userId, correlationId);
                    return ApiResponse<List<OrderResponse>>.SuccessResponse(cachedOrders);
                }

                var orders = await _unitOfWork.Orders.GetByUserIdAsync(userId);
                var orderResponses = orders.Select(MapToOrderResponse).ToList();

                var cacheExpiry = TimeSpan.FromMinutes(CacheKeys.DEFAULT_TTL_MINUTES);
                await _cacheService.SetAsync(cacheKey, orderResponses, cacheExpiry);

                _logger.LogInformation("Retrieved {Count} orders for user {UserId}. CorrelationId: {CorrelationId}",
                    orderResponses.Count, userId, correlationId);

                return ApiResponse<List<OrderResponse>>.SuccessResponse(orderResponses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for user {UserId}. CorrelationId: {CorrelationId}",
                    userId, correlationId);

                return ApiResponse<List<OrderResponse>>.ErrorResponse(
                    "An error occurred while retrieving orders. Please try again later.");
            }
        }

        private static ApiResponse<OrderResponse> ValidateOrderRequest(CreateOrderRequest request)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.UserId))
                errors.Add("User ID is required");

            if (string.IsNullOrWhiteSpace(request.ProductId))
                errors.Add("Product ID is required");

            if (request.Quantity <= 0)
                errors.Add("Quantity must be greater than 0");

            if (!Enum.IsDefined(typeof(PaymentMethod), request.PaymentMethod))
                errors.Add("Invalid payment method");

            if (errors.Any())
            {
                return ApiResponse<OrderResponse>.ErrorResponse("Validation failed", errors);
            }

            return ApiResponse<OrderResponse>.SuccessResponse(null!);
        }

        private static OrderResponse MapToOrderResponse(Order order)
        {
            return new OrderResponse
            {
                Id = order.Id,
                UserId = order.UserId,
                ProductId = order.ProductId,
                Quantity = order.Quantity,
                PaymentMethod = order.PaymentMethod,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            };
        }
    }
}
