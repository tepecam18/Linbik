using Linbik.Core.Extensions;
using Linbik.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Linbik.Core.Services;

/// <summary>
/// Default audit logger implementation that logs to ILogger
/// Can be replaced with custom implementation for database/file/external service logging
/// </summary>
public sealed class DefaultAuditLogger(ILogger<DefaultAuditLogger> logger, IHttpContextAccessor httpContextAccessor) : IAuditLogger
{
    public Task LogAsync(AuditLogEntry entry)
    {
        // Enrich with HTTP context if available
        var context = httpContextAccessor.HttpContext;
        if (context != null)
        {
            entry.IpAddress ??= context.GetClientIpAddress();
            entry.UserAgent ??= context.Request.Headers["User-Agent"].FirstOrDefault();
            entry.CorrelationId ??= context.TraceIdentifier;
        }

        var logLevel = entry.IsSuccess ? LogLevel.Information : LogLevel.Warning;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["AuditEventType"] = entry.EventType.ToString(),
            ["UserId"] = entry.UserId ?? "anonymous",
            ["IpAddress"] = entry.IpAddress ?? "unknown",
            ["CorrelationId"] = entry.CorrelationId ?? "none",
            ["ServiceId"] = entry.ServiceId ?? "none",
            ["DurationMs"] = entry.DurationMs
        }))
        {
            logger.Log(logLevel,
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

}
