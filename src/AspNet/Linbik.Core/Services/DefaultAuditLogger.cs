using Linbik.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Linbik.Core.Services;

/// <summary>
/// Default audit logger implementation that logs to ILogger
/// Can be replaced with custom implementation for database/file/external service logging
/// </summary>
public class DefaultAuditLogger : IAuditLogger
{
    private readonly ILogger<DefaultAuditLogger> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DefaultAuditLogger(ILogger<DefaultAuditLogger> logger, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task LogAsync(AuditLogEntry entry)
    {
        // Enrich with HTTP context if available
        var context = _httpContextAccessor.HttpContext;
        if (context != null)
        {
            entry.IpAddress ??= GetClientIpAddress(context);
            entry.UserAgent ??= context.Request.Headers["User-Agent"].FirstOrDefault();
            entry.CorrelationId ??= context.TraceIdentifier;
        }

        var logLevel = entry.IsSuccess ? LogLevel.Information : LogLevel.Warning;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["AuditEventType"] = entry.EventType.ToString(),
            ["UserId"] = entry.UserId ?? "anonymous",
            ["IpAddress"] = entry.IpAddress ?? "unknown",
            ["CorrelationId"] = entry.CorrelationId ?? "none",
            ["ServiceId"] = entry.ServiceId ?? "none",
            ["DurationMs"] = entry.DurationMs
        }))
        {
            _logger.Log(logLevel,
                "[AUDIT] {EventType} | User: {UserId} | IP: {IpAddress} | Success: {IsSuccess} | Duration: {DurationMs}ms | {Message}",
                entry.EventType,
                entry.UserId ?? "anonymous",
                entry.IpAddress ?? "unknown",
                entry.IsSuccess,
                entry.DurationMs,
                entry.Message ?? string.Empty);
        }

        return Task.CompletedTask;
    }

    public Task LogAsync(AuditEventType eventType, string? userId, string? message = null, bool isSuccess = true, Dictionary<string, object>? additionalData = null)
    {
        return LogAsync(new AuditLogEntry
        {
            EventType = eventType,
            UserId = userId,
            Message = message,
            IsSuccess = isSuccess,
            AdditionalData = additionalData
        });
    }

    public Task LogLoginAttemptAsync(string? userId, string? ipAddress, string? userAgent, bool isSuccess, string? failureReason = null)
    {
        var eventType = isSuccess ? AuditEventType.LoginSuccess : AuditEventType.LoginFailed;
        return LogAsync(new AuditLogEntry
        {
            EventType = eventType,
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IsSuccess = isSuccess,
            Message = failureReason ?? (isSuccess ? "Login successful" : "Login failed")
        });
    }

    public Task LogTokenExchangeAsync(string? userId, string? serviceId, bool isSuccess, long durationMs, string? failureReason = null)
    {
        var eventType = isSuccess ? AuditEventType.TokenExchangeSuccess : AuditEventType.TokenExchangeFailed;
        return LogAsync(new AuditLogEntry
        {
            EventType = eventType,
            UserId = userId,
            ServiceId = serviceId,
            IsSuccess = isSuccess,
            DurationMs = durationMs,
            Message = failureReason ?? (isSuccess ? "Token exchange successful" : "Token exchange failed")
        });
    }

    public Task LogTokenRefreshAsync(string? userId, string? serviceId, bool isSuccess, long durationMs, string? failureReason = null)
    {
        var eventType = isSuccess ? AuditEventType.TokenRefreshSuccess : AuditEventType.TokenRefreshFailed;
        return LogAsync(new AuditLogEntry
        {
            EventType = eventType,
            UserId = userId,
            ServiceId = serviceId,
            IsSuccess = isSuccess,
            DurationMs = durationMs,
            Message = failureReason ?? (isSuccess ? "Token refresh successful" : "Token refresh failed")
        });
    }

    public Task LogRateLimitExceededAsync(string? ipAddress, string? endpoint, string? userId = null)
    {
        return LogAsync(new AuditLogEntry
        {
            EventType = AuditEventType.RateLimitExceeded,
            UserId = userId,
            IpAddress = ipAddress,
            IsSuccess = false,
            Message = $"Rate limit exceeded for endpoint: {endpoint}",
            AdditionalData = new Dictionary<string, object>
            {
                ["Endpoint"] = endpoint ?? "unknown"
            }
        });
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded headers first (for reverse proxy scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain (original client)
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
