using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Request;
using ECommerce.Core.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Application.Services.Interface
{
    public interface IOrderService
    {
        Task<ApiResponse<OrderResponse>> CreateOrderAsync(CreateOrderRequest request, string correlationId);
        Task<ApiResponse<List<OrderResponse>>> GetOrdersByUserIdAsync(string userId, string correlationId);
        Task<ApiResponse<OrderResponse>> GetOrderByIdAsync(string orderId, string correlationId);
    }
}
