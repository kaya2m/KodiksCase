using ECommerce.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Core.DTOs.Request
{
    public class CreateOrderRequest
    {
        [Required(ErrorMessage = "User ID is required")]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product ID is required")]
        public string ProductId { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Payment method is required")]
        public PaymentMethod PaymentMethod { get; set; }
    }
}
