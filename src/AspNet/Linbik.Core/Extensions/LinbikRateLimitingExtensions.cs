using Linbik.Core.Configuration;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;

namespace Linbik.Core.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting for Linbik authentication endpoints.
/// Shared by <c>Linbik.JwtAuthManager</c> and <c>Linbik.PasetoAuthManager</c>.
/// </summary>
public static class LinbikRateLimitingExtensions
{
    /// <summary>
    /// Rate limit policy name for Linbik authentication endpoints.
    /// </summary>
    public const string LinbikAuthPolicy = "LinbikAuth";

    private const string UnknownPartitionKey = "unknown";

    /// <summary>
    /// Marks an <see cref="IServiceCollection"/> that has already had the Linbik rate limiting
    /// policies registered, so a redundant call (explicit or from an AuthManager) is a safe no-op
    /// instead of throwing when the underlying policy name is added twice.
    /// </summary>
    private sealed class LinbikRateLimitingMarker
    {
    }

    /// <summary>
    /// Add rate limiting services configured for Linbik authentication.
    /// </summary>
    public static IServiceCollection AddLinbikRateLimiting(
        this IServiceCollection services, Action<RateLimitOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new RateLimitOptions();
        configureOptions(options);
        services.Configure(configureOptions);
        services.EnsureLinbikRateLimiting(options);
        return services;
    }

    /// <summary>
    /// Add rate limiting services from configuration.
    /// </summary>
    public static IServiceCollection AddLinbikRateLimiting(
        this IServiceCollection services,
        IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = configuration.Get<RateLimitOptions>() ?? new RateLimitOptions();
        services.Configure<RateLimitOptions>(configuration);
        services.EnsureLinbikRateLimiting(options);
        return services;
    }

    /// <summary>
    /// Add rate limiting services using default <see cref="RateLimitOptions"/>.
    /// </summary>
    public static IServiceCollection AddLinbikRateLimiting(this IServiceCollection services)
    {
        var defaultOptions = new RateLimitOptions();
        services.Configure<RateLimitOptions>(_ => { });
        services.EnsureLinbikRateLimiting(defaultOptions);
        return services;
    }

    /// <summary>
    /// Registers the <c>LinbikAuth</c>/<c>LinbikStrict</c>/<c>LinbikGeneral</c> rate limiting policies
    /// exactly once per <see cref="IServiceCollection"/>. Linbik.JwtAuthManager and
    /// Linbik.PasetoAuthManager call this automatically (with default <see cref="RateLimitOptions"/>)
    /// because their endpoints unconditionally require the <see cref="LinbikAuthPolicy"/>/"LinbikStrict"
    /// policies via <c>RequireRateLimiting</c>; without them, requests fail at runtime with
    /// "no such policy exists" instead of at startup. Call <see cref="AddLinbikRateLimiting(IServiceCollection)"/>
    /// (or an overload) explicitly BEFORE <c>AddLinbikJwtAuth</c>/<c>AddLinbikPasetoAuth</c> to customize
    /// limits; a call afterward is a no-op since the policies already exist.
    /// </summary>
    internal static IServiceCollection EnsureLinbikRateLimiting(this IServiceCollection services, RateLimitOptions rateLimitOptions)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(LinbikRateLimitingMarker)))
            return services;

        services.AddSingleton<LinbikRateLimitingMarker>();
        services.AddCommonLinbikRateLimiting(rateLimitOptions);
        return services;
    }

    private static IServiceCollection AddCommonLinbikRateLimiting(this IServiceCollection services, RateLimitOptions rateLimitOptions)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<RateLimitOptions>>();
                var auditLogger = context.HttpContext.RequestServices.GetService<IAuditLogger>();
                var metrics = context.HttpContext.RequestServices.GetService<LinbikMetrics>();

                var ipAddress = context.HttpContext.GetClientIpAddress();
                var endpoint = context.HttpContext.Request.Path.Value;
                // Both JWT (MapInboundClaims=false) and PASETO principals expose the raw "sub" claim.
                var userId = context.HttpContext.User?.FindFirst("sub")?.Value;

                logger?.LogWarning(
                    "Rate limit exceeded for IP {IpAddress} on endpoint {Endpoint}. Retry after: {RetryAfter}",
                    ipAddress, endpoint, context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : TimeSpan.Zero);

                if (auditLogger != null)
                {
                    await auditLogger.LogRateLimitExceededAsync(ipAddress, endpoint, userId);
                }

                metrics?.RecordRateLimitHit(endpoint ?? UnknownPartitionKey);

                context.HttpContext.Response.ContentType = "application/json";

                var retryAfterSeconds = 0;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                {
                    retryAfterSeconds = (int)Math.Clamp(Math.Ceiling(retry.TotalSeconds), 0d, int.MaxValue);
                    context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests",
                    message = "Rate limit exceeded. Please try again later.",
                    retryAfter = retryAfterSeconds
                }, cancellationToken);
            };

            if (rateLimitOptions.UseSlidingWindow)
            {
                options.AddPolicy(LinbikAuthPolicy, context => RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.GetClientIpAddress() ?? UnknownPartitionKey,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                        SegmentsPerWindow = rateLimitOptions.SegmentsPerWindow,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = rateLimitOptions.QueueLimit
                    }));
            }
            else
            {
                options.AddPolicy(LinbikAuthPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.GetClientIpAddress() ?? UnknownPartitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = rateLimitOptions.QueueLimit
                    }));
            }

            options.AddPolicy("LinbikGeneral", context => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.GetClientIpAddress() ?? UnknownPartitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.PermitLimit * rateLimitOptions.GeneralPolicyMultiplier,
                    Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = rateLimitOptions.QueueLimit
                }));

            options.AddPolicy("LinbikStrict", context => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: context.GetClientIpAddress() ?? UnknownPartitionKey,
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = rateLimitOptions.StrictTokenLimit,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(rateLimitOptions.StrictReplenishmentPeriodSeconds),
                    TokensPerPeriod = rateLimitOptions.StrictTokensPerPeriod,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = rateLimitOptions.StrictQueueLimit
                }));
        });

        return services;
    }

    /// <summary>
    /// Use rate limiting middleware.
    /// </summary>
    public static IApplicationBuilder UseLinbikRateLimiting(this IApplicationBuilder app) =>
        app.UseRateLimiter();
}
