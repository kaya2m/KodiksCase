using ECommerce.Core.Interfaces;
using ECommerce.Core.Interfaces.Common;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Repositories;
using ECommerce.Infrastructure.Repositories.Common;
using ECommerce.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerce.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ECommerceDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
        });

        AddRedisCacheServices(services, configuration);

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderProcessingLogRepository, OrderProcessingLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IMessagePublisher, RabbitMQPublisher>();

        return services;
    }

    private static void AddRedisCacheServices(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "ECommerceAPI";
        });

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<IConnectionMultiplexer>>();

            try
            {
                var configurationOptions = ConfigurationOptions.Parse(redisConnectionString);

                configurationOptions.AbortOnConnectFail = false;
                configurationOptions.ConnectRetry = 3;
                configurationOptions.ConnectTimeout = 5000;
                configurationOptions.SyncTimeout = 5000;
                configurationOptions.ReconnectRetryPolicy = new ExponentialRetry(5000);
                configurationOptions.KeepAlive = 180;

                var multiplexer = ConnectionMultiplexer.Connect(configurationOptions);

                multiplexer.ConnectionFailed += (sender, args) =>
                {
                    logger?.LogError("Redis connection failed: {Exception}", args.Exception?.Message);
                };

                multiplexer.ConnectionRestored += (sender, args) =>
                {
                    logger?.LogInformation("Redis connection restored");
                };

                return multiplexer;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to connect to Redis: {ConnectionString}", redisConnectionString);
                throw;
            }
        });

        services.AddScoped<ICacheService, RedisCacheService>();
    }
}
