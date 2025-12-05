using Linbik.Core.Configuration;
using Linbik.Core.Interfaces;
using Linbik.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;

namespace Linbik.JwtAuthManager.Extensions;

/// <summary>
/// Extension methods for configuring Rate Limiting for Linbik authentication endpoints
/// </summary>
public static class RateLimitExtensions
{
    /// <summary>
    /// Rate limit policy name for Linbik authentication endpoints
    /// </summary>
    public const string LinbikAuthPolicy = "LinbikAuth";

    /// <summary>
    /// Add rate limiting services configured for Linbik authentication
    /// </summary>
    public static IServiceCollection AddLinbikRateLimiting(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddRateLimiter(options =>
        {
            // Get rate limit options from configuration
            var rateLimitOptions = new RateLimitOptions();
            configuration?.GetSection("Linbik:RateLimit").Bind(rateLimitOptions);

            // Configure rejection response
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<RateLimitOptions>>();
                var auditLogger = context.HttpContext.RequestServices.GetService<IAuditLogger>();
                var metrics = context.HttpContext.RequestServices.GetService<LinbikMetrics>();

                var ipAddress = GetClientIpAddress(context.HttpContext);
                var endpoint = context.HttpContext.Request.Path.Value;
                var userId = context.HttpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                logger?.LogWarning(
                    "Rate limit exceeded for IP {IpAddress} on endpoint {Endpoint}. Retry after: {RetryAfter}",
                    ipAddress, endpoint, context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : TimeSpan.Zero);

                if (auditLogger != null)
                {
                    await auditLogger.LogRateLimitExceededAsync(ipAddress, endpoint, userId);
                }

                metrics?.RecordRateLimitHit(endpoint ?? "unknown");

                context.HttpContext.Response.ContentType = "application/json";

                var retryAfterSeconds = 0;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                {
                    retryAfterSeconds = (int)retry.TotalSeconds;
                    context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests",
                    message = "Rate limit exceeded. Please try again later.",
                    retryAfter = retryAfterSeconds
                }, cancellationToken);
            };

            // Add fixed window rate limiter for auth endpoints
            if (rateLimitOptions.UseSlidingWindow)
            {
                options.AddSlidingWindowLimiter(LinbikAuthPolicy, limiterOptions =>
                {
                    limiterOptions.PermitLimit = rateLimitOptions.PermitLimit;
                    limiterOptions.Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds);
                    limiterOptions.SegmentsPerWindow = rateLimitOptions.SegmentsPerWindow;
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = rateLimitOptions.QueueLimit;
                });
            }
            else
            {
                options.AddFixedWindowLimiter(LinbikAuthPolicy, limiterOptions =>
                {
                    limiterOptions.PermitLimit = rateLimitOptions.PermitLimit;
                    limiterOptions.Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = rateLimitOptions.QueueLimit;
                });
            }

            // Add a more permissive policy for general endpoints
            options.AddFixedWindowLimiter("LinbikGeneral", limiterOptions =>
            {
                limiterOptions.PermitLimit = rateLimitOptions.PermitLimit * rateLimitOptions.GeneralPolicyMultiplier;
                limiterOptions.Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = rateLimitOptions.QueueLimit;
            });

            // Add a strict policy for sensitive operations like token exchange
            options.AddTokenBucketLimiter("LinbikStrict", limiterOptions =>
            {
                limiterOptions.TokenLimit = rateLimitOptions.StrictTokenLimit;
                limiterOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(rateLimitOptions.StrictReplenishmentPeriodSeconds);
                limiterOptions.TokensPerPeriod = rateLimitOptions.StrictTokensPerPeriod;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = rateLimitOptions.StrictQueueLimit;
            });
        });

        return services;
    }

    /// <summary>
    /// Use rate limiting middleware
    /// </summary>
    public static IApplicationBuilder UseLinbikRateLimiting(this IApplicationBuilder app)
    {
        return app.UseRateLimiter();
    }

    /// <summary>
    /// Get the client IP address from the HTTP context
    /// </summary>
    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded headers first (for reverse proxy scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
