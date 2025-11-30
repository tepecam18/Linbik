namespace Linbik.Core.Interfaces;

/// <summary>
/// Audit event types for authentication operations
/// </summary>
public enum AuditEventType
{
    LoginAttempt,
    LoginSuccess,
    LoginFailed,
    LogoutSuccess,
    TokenExchangeAttempt,
    TokenExchangeSuccess,
    TokenExchangeFailed,
    TokenRefreshAttempt,
    TokenRefreshSuccess,
    TokenRefreshFailed,
    PkceValidationFailed,
    RateLimitExceeded,
    InvalidApiKey,
    InvalidAuthorizationCode,
    SessionCreated,
    SessionExpired
}

/// <summary>
/// Audit log entry with detailed information
/// </summary>
public class AuditLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public AuditEventType EventType { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ServiceId { get; set; }
    public string? ClientId { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object>? AdditionalData { get; set; }
    public bool IsSuccess { get; set; }
    public long DurationMs { get; set; }
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Interface for audit logging of authentication events
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Log an audit event
    /// </summary>
    Task LogAsync(AuditLogEntry entry);

    /// <summary>
    /// Log an audit event with simplified parameters
    /// </summary>
    Task LogAsync(AuditEventType eventType, string? userId, string? message = null, bool isSuccess = true, Dictionary<string, object>? additionalData = null);

    /// <summary>
    /// Log a login attempt
    /// </summary>
    Task LogLoginAttemptAsync(string? userId, string? ipAddress, string? userAgent, bool isSuccess, string? failureReason = null);

    /// <summary>
    /// Log a token exchange event
    /// </summary>
    Task LogTokenExchangeAsync(string? userId, string? serviceId, bool isSuccess, long durationMs, string? failureReason = null);

    /// <summary>
    /// Log a token refresh event
    /// </summary>
    Task LogTokenRefreshAsync(string? userId, string? serviceId, bool isSuccess, long durationMs, string? failureReason = null);

    /// <summary>
    /// Log a rate limit exceeded event
    /// </summary>
    Task LogRateLimitExceededAsync(string? ipAddress, string? endpoint, string? userId = null);
}
