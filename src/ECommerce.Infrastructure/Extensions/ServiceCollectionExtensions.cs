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

namespace ECommerce.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ECommerceDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            options.UseSnakeCaseNamingConvention();
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
        });

        AddRedisCacheServices(services, configuration);

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderProcessingLogRepository, OrderProcessingLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddTransient<IMessagePublisher, RabbitMQPublisher>();

        return services;
    }

    private static void AddRedisCacheServices(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<IConnectionMultiplexer>>();

            try
            {
                if (string.IsNullOrEmpty(redisConnectionString))
                {
                    logger?.LogWarning("Redis connection string is empty, using default localhost:6379");
                    redisConnectionString = "localhost:6379";
                }

                var configurationOptions = ConfigurationOptions.Parse(redisConnectionString);

                configurationOptions.AbortOnConnectFail = false;
                configurationOptions.ConnectRetry = 3;
                configurationOptions.ConnectTimeout = 5000;
                configurationOptions.SyncTimeout = 5000;
                configurationOptions.AsyncTimeout = 5000;
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

                logger?.LogInformation("Redis connection established successfully");
                return multiplexer;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to connect to Redis: {ConnectionString}", redisConnectionString);
                throw new InvalidOperationException($"Redis connection failed: {ex.Message}", ex);
            }
        });

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString ?? "localhost:6379";
            options.InstanceName = "ECommerceAPI";
        });

        services.AddScoped<ICacheService, RedisCacheService>();
    }
    private static IConnectionMultiplexer CreateMockConnection()
    {
        var mockOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectRetry = 0,
            ConnectTimeout = 1000
        };

        try
        {
            return ConnectionMultiplexer.Connect("localhost:6379", opt =>
            {
                opt.AbortOnConnectFail = false;
            });
        }
        catch
        {
            throw new InvalidOperationException("Redis connection could not be established. Please ensure Redis is running.");
        }
    }
}