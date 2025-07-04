using ECommerce.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Core.Interfaces
{
    public interface IOrderRepository
    {
        Task<Order> CreateAsync(Order order);
        Task<Order?> GetByIdAsync(Guid id);
        Task<List<Order>> GetByUserIdAsync(string userId);
        Task UpdateAsync(Order order);
        Task DeleteAsync(Guid id);
    }
}
