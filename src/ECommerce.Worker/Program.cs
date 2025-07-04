using Serilog;
using ECommerce.Shared.Extensions;
using ECommerce.Application.Extensions;
using ECommerce.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ECommerce.Worker")
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();

builder.Services.AddHostedService<OrderProcessingWorker>();

builder.Services.AddHealthChecks()
    .AddCheck<HealthCheckService>("worker_health");

builder.Services.AddSingleton<MessageRetryService>();

var host = builder.Build();

try
{
    Log.Information("Starting Order Processing Worker");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}