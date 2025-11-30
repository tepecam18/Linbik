namespace Linbik.Core.Configuration;

/// <summary>
/// Configuration options for HTTP client resilience (retry, circuit breaker, timeout)
/// </summary>
public class ResilienceOptions
{
    /// <summary>
    /// Enable resilience policies (retry, circuit breaker, timeout)
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts
    /// Default: 3
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retries in milliseconds (exponential backoff)
    /// Default: 1000ms (1 second)
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay between retries in milliseconds
    /// Default: 30000ms (30 seconds)
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 30000;

    /// <summary>
    /// Enable circuit breaker
    /// Default: true
    /// </summary>
    public bool CircuitBreakerEnabled { get; set; } = true;

    /// <summary>
    /// Number of failures before circuit opens
    /// Default: 5
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time window for failure counting in seconds
    /// Default: 30 seconds
    /// </summary>
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Duration the circuit stays open in seconds
    /// Default: 30 seconds
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>
    /// HTTP request timeout in seconds
    /// Default: 30 seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout for individual retry attempts in seconds
    /// Default: 10 seconds
    /// </summary>
    public int AttemptTimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Configuration options for rate limiting
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Enable rate limiting
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Rate limit policy name for authentication endpoints
    /// </summary>
    public string PolicyName { get; set; } = "LinbikAuth";

    /// <summary>
    /// Maximum requests allowed in the time window
    /// Default: 10
    /// </summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>
    /// Time window in seconds
    /// Default: 60 (1 minute)
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Queue limit (requests that can wait when limit is reached)
    /// Default: 0 (no queuing)
    /// </summary>
    public int QueueLimit { get; set; } = 0;

    /// <summary>
    /// Enable sliding window instead of fixed window
    /// Default: false
    /// </summary>
    public bool UseSlidingWindow { get; set; } = false;

    /// <summary>
    /// Number of segments per window for sliding window
    /// Default: 4
    /// </summary>
    public int SegmentsPerWindow { get; set; } = 4;
}

/// <summary>
/// Configuration options for audit logging
/// </summary>
public class AuditOptions
{
    /// <summary>
    /// Enable audit logging
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Log successful operations
    /// Default: true
    /// </summary>
    public bool LogSuccessfulOperations { get; set; } = true;

    /// <summary>
    /// Log failed operations
    /// Default: true
    /// </summary>
    public bool LogFailedOperations { get; set; } = true;

    /// <summary>
    /// Include user agent in logs
    /// Default: true
    /// </summary>
    public bool IncludeUserAgent { get; set; } = true;

    /// <summary>
    /// Include IP address in logs
    /// Default: true
    /// </summary>
    public bool IncludeIpAddress { get; set; } = true;

    /// <summary>
    /// Mask sensitive data in logs (e.g., tokens)
    /// Default: true
    /// </summary>
    public bool MaskSensitiveData { get; set; } = true;
}
