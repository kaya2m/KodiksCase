using ECommerce.Application.Services;
using ECommerce.Application.Services.Interface;
using ECommerce.Core.DTOs.Request;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderProcessingService, OrderProcessingService>();
        return services;
    }
}
