﻿using ECommerce.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Application.Services.Interface
{
    public interface IOrderProcessingService
    {
        Task ProcessOrderAsync(OrderPlacedEvent orderEvent);
    }
}
