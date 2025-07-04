using Microsoft.EntityFrameworkCore;
using ECommerce.Shared.Middleware;
using ECommerce.Infrastructure.Data;
using ECommerce.API.Middleware;
using Serilog;

namespace ECommerce.API.Configuration;

public static class ApplicationBuilderExtensions
{
    public static WebApplication ConfigureApiPipeline(this WebApplication app,
        IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            app.UseSwaggerDocumentation();
            app.UseDeveloperExceptionPage();
            app.ApplyDatabaseMigrations();
        }
        else
        {
            app.UseHsts();
        }

        app.UseSecurityHeaders();
        app.UseResponseCompression();

        app.UseAdvancedLogging(configuration);

        app.UseHttpsRedirection();
        app.UseCors(configuration.GetValue<string>("CorsSettings:PolicyName") ?? "DefaultPolicy");

        if (configuration.GetValue<bool>("RateLimiting:EnableRateLimiting"))
        {
            app.UseRateLimiter();
        }

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<GlobalExceptionMiddleware>();

        if (configuration.GetValue<bool>("FeatureFlags:EnablePerformanceMetrics"))
        {
            app.UseMiddleware<PerformanceMiddleware>();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseRequestLogging();

        app.MapControllers();
        app.MapHealthChecks();

        app.LogStartupInformation(configuration);

        return app;
    }

    private static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");

            await next();
        });

        return app;
    }

    private static WebApplication UseAdvancedLogging(this WebApplication app, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("FeatureFlags:EnableAdvancedLogging"))
        {
            app.UseHttpLogging();
        }

        return app;
    }

    private static WebApplication UseRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.GetLevel = (httpContext, elapsed, ex) => ex != null
                ? Serilog.Events.LogEventLevel.Error
                : elapsed > 10000
                    ? Serilog.Events.LogEventLevel.Warning
                    : Serilog.Events.LogEventLevel.Information;

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]);

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserId", httpContext.User.Identity.Name);
                }
            };
        });

        return app;
    }

    private static WebApplication MapHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false
        });

        return app;
    }

    private static WebApplication ApplyDatabaseMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        try
        {
            if (context.Database.GetPendingMigrations().Any())
            {
                Log.Information("Applying pending database migrations...");
                context.Database.Migrate();
                Log.Information("Database migrations applied successfully");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the database");
        }

        return app;
    }

    private static WebApplication LogStartupInformation(this WebApplication app, IConfiguration configuration)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            Log.Information("=== {ApplicationName} Started ===", configuration["ApiSettings:Title"]);
            Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
            Log.Information("Version: {Version}", configuration["ApiSettings:Version"]);

            if (app.Environment.IsDevelopment())
            {
                Log.Information("Swagger UI: {SwaggerUrl}", "http://localhost:8080");
                Log.Information("Health Check: {HealthUrl}", "http://localhost:8080/health");
            }
        });

        return app;
    }
    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECommerce API V1");
            c.RoutePrefix = string.Empty;
        });

        return app;
    }
}