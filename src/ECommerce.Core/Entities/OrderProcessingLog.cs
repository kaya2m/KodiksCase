using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Core.Entities
{
    public class OrderProcessingLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrderId { get; set; }

        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = string.Empty;

        public string? Message { get; set; }

        public Order Order { get; set; } = null!;
    }
}
