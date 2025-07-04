using ECommerce.Core.Interfaces;
using ECommerce.Core.Interfaces.Common;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Extensions;
using ECommerce.Infrastructure.Repositories;
using ECommerce.Infrastructure.Repositories.Common;
using ECommerce.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddInfrastructureServices(configuration);

            return services;
        }
    }
}
