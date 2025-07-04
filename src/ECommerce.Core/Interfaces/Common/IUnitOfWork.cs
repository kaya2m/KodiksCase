using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Core.Interfaces.Common
{
    public interface IUnitOfWork : IDisposable
    {
        IOrderRepository Orders { get; }
        IOrderProcessingLogRepository OrderProcessingLogs { get; }
        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
