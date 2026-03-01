using Linbik.Server.Configuration;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Linbik.Server.Extensions;

/// <summary>
/// Extension methods for Linbik health check configuration
/// </summary>
public static class HealthCheckExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Add Linbik health checks to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Optional configuration action</param>
    /// <returns>IHealthChecksBuilder for chaining additional health checks</returns>
    public static IHealthChecksBuilder AddLinbikHealthChecks(
        this IServiceCollection services,
        Action<LinbikHealthCheckOptions>? configureOptions = null)
    {
        var options = new LinbikHealthCheckOptions();
        configureOptions?.Invoke(options);
        services.Configure<LinbikHealthCheckOptions>(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.HealthPath = options.HealthPath;
            opt.ReadyPath = options.ReadyPath;
            opt.LivePath = options.LivePath;
            opt.IncludeDetails = options.IncludeDetails;
            opt.ReadyTags = options.ReadyTags;
            opt.LiveTags = options.LiveTags;
        });

        return services.AddHealthChecks()
            // Liveness: Is the app running?
            .AddCheck<LinbikLivenessHealthCheck>(
                "linbik_liveness",
                HealthStatus.Unhealthy,
                options.LiveTags)
            // Readiness: Is the app ready to accept traffic?
            .AddCheck<LinbikReadinessHealthCheck>(
                "linbik_readiness",
                HealthStatus.Unhealthy,
                options.ReadyTags)
            // Auth: Is JWT validation configured?
            .AddCheck<LinbikAuthHealthCheck>(
                "linbik_auth",
                HealthStatus.Degraded,
                ["ready", "auth"]);
    }

    /// <summary>
    /// Add Linbik health checks from configuration
    public static IHealthChecksBuilder AddLinbikHealthChecks(
        this IServiceCollection services,
        IConfigurationSection configuration)
    {
        var options = configuration.Get<LinbikHealthCheckOptions>()
            ?? new LinbikHealthCheckOptions();

        return services.AddLinbikHealthChecks(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.HealthPath = options.HealthPath;
            opt.ReadyPath = options.ReadyPath;
            opt.LivePath = options.LivePath;
            opt.IncludeDetails = options.IncludeDetails;
            opt.ReadyTags = options.ReadyTags;
            opt.LiveTags = options.LiveTags;
        });
    }

    /// <summary>
    /// Map Linbik health check endpoints
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <param name="configureOptions">Optional configuration action</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseLinbikHealthChecks(
        this IApplicationBuilder app,
        Action<LinbikHealthCheckOptions>? configureOptions = null)
    {
        var options = new LinbikHealthCheckOptions();
        configureOptions?.Invoke(options);

        if (!options.Enabled)
            return app;

        // /health - General health status
        app.UseHealthChecks(options.HealthPath, new HealthCheckOptions
        {
            ResponseWriter = (context, report) => WriteHealthCheckResponse(context, report, options.IncludeDetails)
        });

        // /ready - Kubernetes readiness probe
        app.UseHealthChecks(options.ReadyPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = (context, report) => WriteHealthCheckResponse(context, report, options.IncludeDetails)
        });

        // /live - Kubernetes liveness probe
        app.UseHealthChecks(options.LivePath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = (context, report) => WriteHealthCheckResponse(context, report, options.IncludeDetails)
        });

        return app;
    }

    /// <summary>
    /// Write health check response as JSON
    /// </summary>
    private static Task WriteHealthCheckResponse(
        HttpContext context,
        HealthReport report,
        bool includeDetails)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTime.UtcNow,
            checks = includeDetails
                ? report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds,
                    description = e.Value.Description,
                    data = e.Value.Data,
                    exception = e.Value.Exception?.Message
                })
                : null
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
