using AspNet.Models;
using Linbik.Core.Interfaces;
using Linbik.Core.Models;
using Linbik.Core.Services;
using Linbik.JwtAuthManager.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AspNet.Controllers;

public class TestController : Controller
{
    private const string AuthTokenCookie = "authToken";
    private const string UserNameCookie = "userName";
    private const string IntegrationTokenPrefix = "integration_";

    private readonly IAuditLogger _auditLogger;
    private readonly LinbikMetrics _metrics;

    public TestController(IAuditLogger auditLogger, LinbikMetrics metrics)
    {
        _auditLogger = auditLogger;
        _metrics = metrics;
    }

    /// <summary>
    /// Ana dashboard sayfası - Kullanıcı durumu ve token bilgilerini gösterir
    /// </summary>
    public IActionResult Index()
    {
        UserProfile? profile = null;
        var tokens = new List<LinbikIntegrationToken>();

        // Check for auth token cookie
        var authToken = Request.Cookies[AuthTokenCookie];
        if (!string.IsNullOrEmpty(authToken))
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(authToken);
                
                var userId = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var userName = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
                var displayName = jwt.Claims.FirstOrDefault(c => c.Type == "display_name")?.Value;

                if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
                {
                    profile = new UserProfile
                    {
                        UserId = userGuid,
                        UserName = userName ?? string.Empty,
                        NickName = displayName ?? userName ?? string.Empty
                    };
                }
            }
            catch
            {
                // Invalid token, user not logged in
            }
        }

        // Get integration tokens from cookies
        foreach (var cookie in Request.Cookies)
        {
            if (cookie.Key.StartsWith(IntegrationTokenPrefix))
            {
                var packageName = cookie.Key.Substring(IntegrationTokenPrefix.Length);
                tokens.Add(new LinbikIntegrationToken
                {
                    PackageName = packageName,
                    ServiceName = packageName,
                    Token = cookie.Value ?? string.Empty,
                    ServiceUrl = string.Empty
                });
            }
        }

        var model = new DashboardViewModel
        {
            IsLoggedIn = profile != null,
            Profile = profile,
            Tokens = tokens
        };

        return View(model);
    }

    /// <summary>
    /// Rate limiting test endpoint - LinbikAuth policy (10 req/min)
    /// </summary>
    [EnableRateLimiting("LinbikAuth")]
    [HttpGet]
    public async Task<IActionResult> TestRateLimit()
    {
        // Record metrics
        _metrics.RecordLoginAttempt("test-client");

        // Log the test access
        await _auditLogger.LogAsync(
            AuditEventType.LoginAttempt,
            userId: null,
            message: "Rate limit test endpoint accessed",
            isSuccess: true,
            additionalData: new Dictionary<string, object>
            {
                ["endpoint"] = "TestRateLimit",
                ["timestamp"] = DateTime.UtcNow
            });

        return Json(new
        {
            success = true,
            message = "Rate limit test successful!",
            timestamp = DateTime.UtcNow,
            policy = "LinbikAuth (10 requests per minute)"
        });
    }

    /// <summary>
    /// Strict rate limiting test - LinbikStrict policy (Token Bucket: 5 tokens)
    /// </summary>
    [EnableRateLimiting("LinbikStrict")]
    [HttpGet]
    public async Task<IActionResult> TestStrictRateLimit()
    {
        _metrics.RecordLoginAttempt("strict-test-client");

        await _auditLogger.LogAsync(
            AuditEventType.LoginAttempt,
            userId: null,
            message: "Strict rate limit test endpoint accessed",
            isSuccess: true);

        return Json(new
        {
            success = true,
            message = "Strict rate limit test successful!",
            timestamp = DateTime.UtcNow,
            policy = "LinbikStrict (Token Bucket: 5 tokens, 2 refill per second)"
        });
    }

    /// <summary>
    /// Metrics dashboard - Shows current metrics values
    /// </summary>
    [HttpGet]
    public IActionResult Metrics()
    {
        // Get current metrics snapshot
        var metricsData = new
        {
            description = "Linbik Metrics Overview",
            note = "These metrics are collected via System.Diagnostics.Metrics and can be exported to Prometheus/Grafana via OpenTelemetry",
            availableMetrics = new[]
            {
                new { name = "linbik.login.attempts", type = "Counter", description = "Total login attempts" },
                new { name = "linbik.login.successes", type = "Counter", description = "Successful logins" },
                new { name = "linbik.login.failures", type = "Counter", description = "Failed logins" },
                new { name = "linbik.token.exchanges", type = "Counter", description = "Token exchange operations" },
                new { name = "linbik.token.refreshes", type = "Counter", description = "Token refresh operations" },
                new { name = "linbik.rate_limit.hits", type = "Counter", description = "Rate limit exceeded events" },
                new { name = "linbik.login.duration", type = "Histogram", description = "Login operation duration (ms)" },
                new { name = "linbik.token.duration", type = "Histogram", description = "Token operation duration (ms)" }
            },
            openTelemetrySetup = new
            {
                meterName = "Linbik.Core",
                exporters = new[] { "Prometheus", "OTLP", "Console" },
                example = "builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter(\"Linbik.Core\"))"
            }
        };

        return Json(metricsData);
    }

    /// <summary>
    /// Audit log test - Demonstrates audit logging
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> TestAuditLog([FromBody] AuditTestRequest? request)
    {
        var eventType = request?.EventType ?? "LoginAttempt";
        var message = request?.Message ?? "Test audit log entry";

        // Parse event type
        if (!Enum.TryParse<AuditEventType>(eventType, true, out var auditEventType))
        {
            auditEventType = AuditEventType.LoginAttempt;
        }

        await _auditLogger.LogAsync(
            auditEventType,
            userId: null,
            message: message,
            isSuccess: true,
            additionalData: new Dictionary<string, object>
            {
                ["source"] = "TestController",
                ["requestedEventType"] = eventType,
                ["testData"] = request?.TestData ?? new { }
            });

        return Json(new
        {
            success = true,
            logged = true,
            eventType = auditEventType.ToString(),
            message = message,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Security info endpoint - Shows current security configuration
    /// </summary>
    [HttpGet]
    public IActionResult SecurityInfo()
    {
        var securityInfo = new
        {
            rateLimiting = new
            {
                enabled = true,
                policies = new[]
                {
                    new { name = "LinbikAuth", type = "FixedWindow", limit = "10 requests/minute" },
                    new { name = "LinbikStrict", type = "TokenBucket", limit = "5 tokens, 2 refill/sec" },
                    new { name = "LinbikGeneral", type = "FixedWindow", limit = "50 requests/minute" }
                }
            },
            httpResilience = new
            {
                enabled = true,
                features = new[]
                {
                    "Retry with exponential backoff (3 attempts)",
                    "Circuit breaker (5 failures = 30 sec break)",
                    "Request timeout (30 seconds)"
                }
            },
            auditLogging = new
            {
                enabled = true,
                eventTypes = Enum.GetNames<AuditEventType>(),
                features = new[]
                {
                    "Structured logging",
                    "IP address capture (X-Forwarded-For support)",
                    "User agent logging",
                    "Correlation ID tracking"
                }
            },
            cookies = new
            {
                settings = new
                {
                    httpOnly = true,
                    secure = true,
                    sameSite = "None",
                    path = "/"
                }
            }
        };

        return Json(securityInfo);
    }
}

public class AuditTestRequest
{
    public string? EventType { get; set; }
    public string? Message { get; set; }
    public object? TestData { get; set; }
}
