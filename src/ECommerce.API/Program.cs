using Serilog;
using ECommerce.API.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

app.ConfigureApiPipeline(builder.Configuration, builder.Environment);

try
{
    Log.Information("Starting E-Commerce API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}