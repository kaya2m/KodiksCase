using ECommerce.Application.Extensions;
using ECommerce.Shared.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;


namespace ECommerce.API.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services,
        IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddControllers(options =>
        {
            options.SuppressAsyncSuffixInActionNames = false;
        });

        services.AddEndpointsApiExplorer();

        services.AddInfrastructure(configuration);
        services.AddApplicationServices();

        services.AddSwaggerDocumentation(configuration);
        services.AddJwtAuthentication(configuration);
        services.AddCorsConfiguration(configuration);
        services.AddRateLimitingConfiguration(configuration);
        services.AddHealthChecksConfiguration(configuration);
        services.AddCachingConfiguration(configuration);
        services.AddCompressionConfiguration();
        services.AddLoggingConfiguration(configuration);

        return services;
    }
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"];
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var clockSkew = TimeSpan.FromMinutes(jwtSettings.GetValue<int>("ClockSkewMinutes"));

        if (string.IsNullOrEmpty(secretKey))
        {
            throw new InvalidOperationException("JWT SecretKey is not configured");
        }

        var key = Encoding.UTF8.GetBytes(secretKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = configuration.GetValue<bool>("Security:RequireHttps");

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = clockSkew,
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogWarning("JWT Authentication failed: {Error}", context.Exception.Message);

                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Add("Token-Expired", "true");
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogDebug("JWT Token validated successfully for user: {User}",
                        context.Principal?.Identity?.Name ?? "Unknown");
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogWarning("JWT Challenge triggered: {Error}", context.Error);
                    return Task.CompletedTask;
                },
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hub"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAuthenticatedUser", policy =>
            {
                policy.RequireAuthenticatedUser();
            });

            options.AddPolicy("AdminOnly", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("role", "Admin");
            });

            options.AddPolicy("ManagerOrAdmin", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("role", "Admin", "Manager");
            });
        });

        return services;
    }
    private static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ECommerce API",
                Version = "v1",
                Description = "API documentation for the ECommerce application"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter 'Bearer' [space] and then your token in the text input below."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
    private static IServiceCollection AddCorsConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection("CorsSettings");
        var policyName = corsSettings["PolicyName"] ?? "DefaultPolicy";

        services.AddCors(options =>
        {
            options.AddPolicy(policyName, builder =>
            {
                var origins = corsSettings.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
                var methods = corsSettings.GetSection("AllowedMethods").Get<string[]>() ??
                    new[] { "GET", "POST", "PUT", "DELETE" };
                var headers = corsSettings.GetSection("AllowedHeaders").Get<string[]>() ??
                    new[] { "Content-Type", "Authorization" };

                if (origins.Length > 0)
                {
                    builder.WithOrigins(origins);
                }
                else
                {
                    builder.AllowAnyOrigin();
                }

                builder.WithMethods(methods)
                       .WithHeaders(headers);

                if (corsSettings.GetValue<bool>("AllowCredentials"))
                {
                    builder.AllowCredentials();
                }

                builder.SetPreflightMaxAge(TimeSpan.FromSeconds(corsSettings.GetValue<int>("MaxAge")));
            });
        });

        return services;
    }

    private static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        var rateLimitSettings = configuration.GetSection("RateLimiting");

        if (!rateLimitSettings.GetValue<bool>("EnableRateLimiting"))
            return services;

        services.AddRateLimiter(options =>
        {
            // Global policy
            var globalPolicy = rateLimitSettings.GetSection("GlobalPolicy");
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = globalPolicy.GetValue<int>("PermitLimit"),
                        Window = globalPolicy.GetValue<TimeSpan>("Window"),
                        QueueLimit = globalPolicy.GetValue<int>("QueueLimit")
                    }));

            // Authenticated user policy
            var authPolicy = rateLimitSettings.GetSection("AuthenticatedUserPolicy");
            options.AddPolicy("AuthenticatedUser", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = authPolicy.GetValue<int>("PermitLimit"),
                        Window = authPolicy.GetValue<TimeSpan>("Window"),
                        QueueLimit = authPolicy.GetValue<int>("QueueLimit")
                    }));

            options.RejectionStatusCode = 429;
        });

        return services;
    }

    private static IServiceCollection AddHealthChecksConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthCheckSettings = configuration.GetSection("HealthChecks");

        services.AddHealthChecks()
            .AddNpgSql(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "postgresql",
                timeout: healthCheckSettings.GetValue<TimeSpan>("Database:Timeout"),
                tags: new[] { "database", "ready" })
            .AddRedis(
                configuration.GetConnectionString("Redis")!,
                name: "redis",
                timeout: healthCheckSettings.GetValue<TimeSpan>("Redis:Timeout"),
                tags: new[] { "cache", "ready" });

        return services;
    }

    private static IServiceCollection AddCachingConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        var cachingSettings = configuration.GetSection("Caching");

        if (cachingSettings.GetValue<bool>("EnableMemoryCache"))
        {
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = cachingSettings.GetValue<int>("MemoryCacheLimitMB") * 1024 * 1024;
            });
        }

        return services;
    }

    private static IServiceCollection AddCompressionConfiguration(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });

        return services;
    }

    private static IServiceCollection AddLoggingConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("FeatureFlags:EnableAdvancedLogging"))
        {
            services.AddHttpLogging(logging =>
            {
                logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPropertiesAndHeaders |
                                      Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponsePropertiesAndHeaders;
                logging.RequestHeaders.Add("X-Correlation-ID");
                logging.ResponseHeaders.Add("X-Correlation-ID");
                logging.MediaTypeOptions.AddText("application/json");
                logging.RequestBodyLogLimit = 4096;
                logging.ResponseBodyLogLimit = 4096;
            });
        }

        return services;
    }
}