using ECommerce.Core.Entities;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace ECommerce.Infrastructure.Repositories
{
    public class OrderProcessingLogRepository : IOrderProcessingLogRepository
    {
        private readonly ECommerceDbContext _context;

        public OrderProcessingLogRepository(ECommerceDbContext context)
        {
            _context = context;
        }

        public async Task<OrderProcessingLog> CreateAsync(OrderProcessingLog log)
        {
            await _context.OrderProcessingLogs.AddAsync(log);
            return log;
        }

        public async Task<List<OrderProcessingLog>> GetByOrderIdAsync(Guid orderId)
        {
            return await _context.OrderProcessingLogs
                .Where(l => l.OrderId == orderId)
                .OrderByDescending(l => l.ProcessedAt)
                .ToListAsync();
        }
    }
}
