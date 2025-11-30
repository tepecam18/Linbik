using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Linbik.Core.Services;

/// <summary>
/// Performance metrics service using System.Diagnostics.Metrics
/// Integrates with OpenTelemetry, Prometheus, Azure Monitor, etc.
/// </summary>
public sealed class LinbikMetrics : IDisposable
{
    public const string MeterName = "Linbik.Auth";

    private readonly Meter _meter;
    private readonly Counter<long> _loginAttempts;
    private readonly Counter<long> _loginSuccesses;
    private readonly Counter<long> _loginFailures;
    private readonly Counter<long> _tokenExchanges;
    private readonly Counter<long> _tokenExchangeFailures;
    private readonly Counter<long> _tokenRefreshes;
    private readonly Counter<long> _tokenRefreshFailures;
    private readonly Counter<long> _rateLimitHits;
    private readonly Histogram<double> _tokenExchangeDuration;
    private readonly Histogram<double> _tokenRefreshDuration;
    private readonly Histogram<double> _authCallbackDuration;

    public LinbikMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        // Login metrics
        _loginAttempts = _meter.CreateCounter<long>(
            "linbik_login_attempts_total",
            description: "Total number of login attempts");

        _loginSuccesses = _meter.CreateCounter<long>(
            "linbik_login_successes_total",
            description: "Total number of successful logins");

        _loginFailures = _meter.CreateCounter<long>(
            "linbik_login_failures_total",
            description: "Total number of failed logins");

        // Token exchange metrics
        _tokenExchanges = _meter.CreateCounter<long>(
            "linbik_token_exchanges_total",
            description: "Total number of token exchange operations");

        _tokenExchangeFailures = _meter.CreateCounter<long>(
            "linbik_token_exchange_failures_total",
            description: "Total number of failed token exchanges");

        _tokenExchangeDuration = _meter.CreateHistogram<double>(
            "linbik_token_exchange_duration_seconds",
            unit: "s",
            description: "Duration of token exchange operations in seconds");

        // Token refresh metrics
        _tokenRefreshes = _meter.CreateCounter<long>(
            "linbik_token_refreshes_total",
            description: "Total number of token refresh operations");

        _tokenRefreshFailures = _meter.CreateCounter<long>(
            "linbik_token_refresh_failures_total",
            description: "Total number of failed token refreshes");

        _tokenRefreshDuration = _meter.CreateHistogram<double>(
            "linbik_token_refresh_duration_seconds",
            unit: "s",
            description: "Duration of token refresh operations in seconds");

        // Auth callback duration
        _authCallbackDuration = _meter.CreateHistogram<double>(
            "linbik_auth_callback_duration_seconds",
            unit: "s",
            description: "Duration of authentication callback processing in seconds");

        // Rate limiting metrics
        _rateLimitHits = _meter.CreateCounter<long>(
            "linbik_rate_limit_hits_total",
            description: "Total number of rate limit hits");
    }

    public void RecordLoginAttempt(string? clientId = null)
    {
        var tags = new TagList { { "client_id", clientId ?? "unknown" } };
        _loginAttempts.Add(1, tags);
    }

    public void RecordLoginSuccess(string? clientId = null)
    {
        var tags = new TagList { { "client_id", clientId ?? "unknown" } };
        _loginSuccesses.Add(1, tags);
    }

    public void RecordLoginFailure(string reason, string? clientId = null)
    {
        var tags = new TagList
        {
            { "client_id", clientId ?? "unknown" },
            { "reason", reason }
        };
        _loginFailures.Add(1, tags);
    }

    public void RecordTokenExchange(bool success, double durationSeconds, string? serviceId = null)
    {
        var tags = new TagList
        {
            { "service_id", serviceId ?? "unknown" },
            { "success", success.ToString().ToLower() }
        };

        _tokenExchanges.Add(1, tags);
        _tokenExchangeDuration.Record(durationSeconds, tags);

        if (!success)
        {
            _tokenExchangeFailures.Add(1, tags);
        }
    }

    public void RecordTokenRefresh(bool success, double durationSeconds, string? serviceId = null)
    {
        var tags = new TagList
        {
            { "service_id", serviceId ?? "unknown" },
            { "success", success.ToString().ToLower() }
        };

        _tokenRefreshes.Add(1, tags);
        _tokenRefreshDuration.Record(durationSeconds, tags);

        if (!success)
        {
            _tokenRefreshFailures.Add(1, tags);
        }
    }

    public void RecordAuthCallbackDuration(double durationSeconds, bool success)
    {
        var tags = new TagList { { "success", success.ToString().ToLower() } };
        _authCallbackDuration.Record(durationSeconds, tags);
    }

    public void RecordRateLimitHit(string endpoint, string? clientId = null)
    {
        var tags = new TagList
        {
            { "endpoint", endpoint },
            { "client_id", clientId ?? "unknown" }
        };
        _rateLimitHits.Add(1, tags);
    }

    /// <summary>
    /// Create a timer for measuring operation duration
    /// </summary>
    public OperationTimer StartTimer() => new();

    public void Dispose()
    {
        _meter.Dispose();
    }
}

/// <summary>
/// Helper class for timing operations
/// </summary>
public sealed class OperationTimer
{
    private readonly Stopwatch _stopwatch;

    public OperationTimer()
    {
        _stopwatch = Stopwatch.StartNew();
    }

    public double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;
    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

    public void Stop() => _stopwatch.Stop();
}
