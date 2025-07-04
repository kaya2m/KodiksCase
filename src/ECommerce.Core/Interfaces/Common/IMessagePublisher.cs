using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Core.Interfaces.Common
{
    public interface IMessagePublisher
    {
        Task PublishAsync<T>(T message, string queueName) where T : class;
    }
}
