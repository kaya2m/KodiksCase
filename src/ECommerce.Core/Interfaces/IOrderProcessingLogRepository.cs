using ECommerce.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Core.Interfaces
{
    public interface IOrderProcessingLogRepository
    {
        Task<OrderProcessingLog> CreateAsync(OrderProcessingLog log);
        Task<List<OrderProcessingLog>> GetByOrderIdAsync(Guid orderId);
    }
}
