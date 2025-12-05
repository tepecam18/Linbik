using Linbik.Core.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Linbik.Core.Extensions;

/// <summary>
/// Extension methods for adding Linbik health checks to the application.
/// </summary>
public static class LinbikHealthCheckExtensions
{
    /// <summary>
    /// The name of the Linbik server health check.
    /// </summary>
    public const string LinbikServerHealthCheckName = "linbik-server";

    /// <summary>
    /// Adds Linbik health checks to the service collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method registers health checks for:
    /// <list type="bullet">
    ///   <item><description>Linbik server connectivity</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The health checks can be exposed via the <c>MapHealthChecks</c> middleware.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="tags">Optional tags to associate with the health checks.</param>
    /// <param name="failureStatus">The failure status to report when health check fails.</param>
    /// <returns>The health checks builder for further configuration.</returns>
    /// <example>
    /// <code>
    /// // Basic usage
    /// builder.Services.AddLinbikHealthChecks();
    /// 
    /// // With custom tags
    /// builder.Services.AddLinbikHealthChecks(tags: new[] { "ready", "live" });
    /// 
    /// // With custom failure status
    /// builder.Services.AddLinbikHealthChecks(failureStatus: HealthStatus.Degraded);
    /// 
    /// // In the pipeline
    /// app.MapHealthChecks("/health", new HealthCheckOptions
    /// {
    ///     Predicate = check => check.Tags.Contains("ready")
    /// });
    /// </code>
    /// </example>
    public static IHealthChecksBuilder AddLinbikHealthChecks(
        this IServiceCollection services,
        IEnumerable<string>? tags = null,
        HealthStatus failureStatus = HealthStatus.Unhealthy)
    {
        var healthCheckTags = tags?.ToArray() ?? ["linbik"];

        // Register the health check HTTP client
        services.AddHttpClient("LinbikHealthCheck", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services.AddHealthChecks()
            .AddCheck<LinbikServerHealthCheck>(
                LinbikServerHealthCheckName,
                failureStatus,
                healthCheckTags);
    }

    /// <summary>
    /// Adds Linbik health checks to an existing health checks builder.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="services">The service collection (needed for HTTP client registration).</param>
    /// <param name="tags">Optional tags to associate with the health checks.</param>
    /// <param name="failureStatus">The failure status to report when health check fails.</param>
    /// <returns>The health checks builder for further configuration.</returns>
    public static IHealthChecksBuilder AddLinbikHealthChecks(
        this IHealthChecksBuilder builder,
        IServiceCollection services,
        IEnumerable<string>? tags = null,
        HealthStatus failureStatus = HealthStatus.Unhealthy)
    {
        var healthCheckTags = tags?.ToArray() ?? ["linbik"];

        // Register the health check HTTP client
        services.AddHttpClient("LinbikHealthCheck", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return builder.AddCheck<LinbikServerHealthCheck>(
            LinbikServerHealthCheckName,
            failureStatus,
            healthCheckTags);
    }
}
