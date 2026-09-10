using System.Globalization;
using AspNet.Models;
using Linbik.Core.Attributes;
using Linbik.Core.Models;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AspNet.Controllers;

public sealed class TestController(
    LinbikMetrics metrics,
    IPasetoHelper pasetoHelper,
    IHttpClientFactory httpClientFactory) : Controller
{
    private const string AuthTokenCookie = Linbik.Core.LinbikDefaults.AuthTokenCookie;
    private const string RefreshTokenCookie = Linbik.Core.LinbikDefaults.RefreshTokenCookie;
    private const string IntegrationTokenPrefix = Linbik.Core.LinbikDefaults.IntegrationTokenPrefix;

    private static DateTime? ParseUnixSeconds(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt;
        return null;
    }

    /// <summary>
    /// Ana dashboard sayfası - Kullanıcı durumu ve token bilgilerini gösterir
    /// </summary>
    public async Task<IActionResult> Index()
    {
        UserProfile? profile = null;
        List<LinbikIntegrationToken> tokens = [];

        // This page allows anonymous visitors, so authenticate the browser scheme explicitly.
        // Protected actions select the same scheme through [LinbikAuthorize].
        var authentication = await HttpContext.AuthenticateAsync(Linbik.Core.LinbikDefaults.ClientScheme);
        if (authentication.Succeeded && authentication.Principal is { } principal)
        {
            HttpContext.User = principal;
            var userId = principal.FindFirst("sub")?.Value;
            var userName = principal.FindFirst("preferred_username")?.Value;
            var displayName = principal.FindFirst("name")?.Value;

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

    #region Authentication Tests

    /// <summary>
    /// Protected endpoint - Requires valid Linbik JWT token in cookie
    /// Uses [LinbikAuthorize] attribute for authentication
    /// </summary>
    [LinbikAuthorize]
    [HttpGet]
    public IActionResult Protected()
    {
        var userId = User.FindFirst("sub")?.Value;
        var userName = User.FindFirst("preferred_username")?.Value;
        var displayName = User.FindFirst("name")?.Value;

        return Json(new
        {
            success = true,
            message = "✅ [LinbikAuthorize] ile korunan endpoint'e erişildi!",
            authScheme = "LinbikScheme (Cookie JWT - HS256)",
            user = new { userId, userName, displayName },
            claimCount = User.Claims.Count(),
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Profile endpoint - Returns user profile from JWT claims
    /// </summary>
    [LinbikAuthorize]
    [HttpGet]
    public IActionResult Profile()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

        return Json(new
        {
            userId = User.FindFirst("sub")?.Value,
            userName = User.FindFirst("preferred_username")?.Value,
            displayName = User.FindFirst("name")?.Value,
            email = User.FindFirst("email")?.Value,
            isAuthenticated = User.Identity?.IsAuthenticated ?? false,
            authenticationType = User.Identity?.AuthenticationType,
            allClaims = claims
        });
    }

    #endregion

    #region Refresh Token Test

    /// <summary>
    /// Refresh token test - Attempts to refresh the current session
    /// </summary>
    [HttpPost]
    public IActionResult TestRefreshToken()
    {
        var options = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Linbik.PasetoAuthManager.Configuration.PasetoAuthOptions>>();
        return RedirectPreserveMethod(options.Value.RefreshPath);
    }
    #endregion

    #region Rate Limiting Tests

    /// <summary>
    /// Rate limiting test - LinbikAuth policy (10 req/min)
    /// Hızlı tıklayarak test edin - 10 istekten sonra 429 hatası almalısınız
    /// </summary>
    [EnableRateLimiting("LinbikAuth")]
    [HttpGet]
    public IActionResult TestRateLimit()
    {
        metrics.RecordLoginAttempt("rate-test");

        return Json(new
        {
            success = true,
            message = "✅ Rate limit testi başarılı!",
            policy = "LinbikAuth",
            limit = "10 istek / dakika",
            tip = "11. istekte 429 Too Many Requests almalısınız",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Strict rate limiting test - Token Bucket (5 tokens, 2 refill/10sec)
    /// </summary>
    [EnableRateLimiting("LinbikStrict")]
    [HttpGet]
    public IActionResult TestStrictRateLimit()
    {
        metrics.RecordLoginAttempt("strict-test");

        return Json(new
        {
            success = true,
            message = "✅ Strict rate limit testi başarılı!",
            policy = "LinbikStrict (Token Bucket)",
            limit = "5 token, 10 saniyede 2 token yenilenir",
            tip = "6. istekte 429 almalısınız, 10 saniye bekleyince 2 istek daha yapabilirsiniz",
            timestamp = DateTime.UtcNow
        });
    }

    #endregion

    #region Integration Service Mock Test

    /// <summary>
    /// Mock Integration Service endpoint - Tests [LinbikDelegatedAuthorize]
    /// Bu endpoint, gerçek bir integration service'in nasıl JWT doğrulaması yapacağını simüle eder.
    /// Authorization header'dan Bearer token bekler (RSA-256 ile imzalanmış)
    /// </summary>
    [HttpGet]
    public IActionResult MockIntegrationEndpoint([FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized(new
            {
                success = false,
                error = "Authorization header gerekli",
                expectedFormat = "Authorization: Bearer {jwt_token}",
                message = "Bu endpoint bir integration service'i simüle eder. integration_xxx cookie'sindeki token'ı Authorization header'a eklemeniz gerekir."
            });
        }

        var token = authHeader.Substring("Bearer ".Length);

        try
        {
            var claims = pasetoHelper.GetTokenClaims(token);

            // Token bilgilerini göster (validation yapmadan - sadece demo)
            return Json(new
            {
                success = true,
                message = "✅ Integration service mock endpoint'ine erişildi!",
                note = "Gerçek integration service Ed25519 public key ile PASETO doğrulaması yapar",
                tokenInfo = new
                {
                    issuer = claims.GetValueOrDefault("iss"),
                    audience = claims.GetValueOrDefault("aud"),
                    subject = claims.GetValueOrDefault("sub"),
                    issuedAt = ParseUnixSeconds(claims.GetValueOrDefault("iat")),
                    expires = ParseUnixSeconds(claims.GetValueOrDefault("exp")),
                    claims = claims.Select(c => new { Type = c.Key, c.Value })
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                error = "Geçersiz PASETO token",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Integration token test - Cookie'deki integration token'ı kullanarak mock endpoint'i çağırır
    /// </summary>
    [HttpGet]
    public IActionResult TestIntegrationToken([FromQuery] string? packageName)
    {
        if (string.IsNullOrEmpty(packageName))
        {
            // Mevcut integration token'ları listele
            var availableTokens = Request.Cookies
                .Where(c => c.Key.StartsWith(IntegrationTokenPrefix))
                .Select(c => c.Key.Substring(IntegrationTokenPrefix.Length))
                .ToList();

            if (!availableTokens.Any())
            {
                return Json(new
                {
                    success = false,
                    error = "Integration token bulunamadı",
                    message = "Giriş yapın ve integration servislerine izin verin",
                    cookiePrefix = IntegrationTokenPrefix
                });
            }

            return Json(new
            {
                success = true,
                message = "Mevcut integration token'lar",
                availablePackages = availableTokens,
                usage = $"/Test/TestIntegrationToken?packageName={availableTokens.First()}"
            });
        }

        var cookieName = $"{IntegrationTokenPrefix}{packageName}";
        var token = Request.Cookies[cookieName];

        if (string.IsNullOrEmpty(token))
        {
            return Json(new
            {
                success = false,
                error = $"'{packageName}' için integration token bulunamadı",
                cookieName = cookieName
            });
        }

        try
        {
            var claims = pasetoHelper.GetTokenClaims(token);
            var expires = ParseUnixSeconds(claims.GetValueOrDefault("exp"));

            return Json(new
            {
                success = true,
                packageName = packageName,
                message = "✅ Integration token bulundu ve decode edildi!",
                note = "Bu token, integration service'e istek yaparken Authorization header'a eklenir",
                tokenInfo = new
                {
                    issuer = claims.GetValueOrDefault("iss"),
                    audience = claims.GetValueOrDefault("aud"),
                    subject = claims.GetValueOrDefault("sub"),
                    issuedAt = ParseUnixSeconds(claims.GetValueOrDefault("iat")),
                    expires,
                    isExpired = expires.HasValue && expires.Value < DateTime.UtcNow,
                    claimCount = claims.Count
                },
                usage = new
                {
                    header = "Authorization: Bearer {token}",
                    proxyEndpoint = $"/{packageName}/api/..."
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                error = "Token decode hatası",
                message = ex.Message
            });
        }
    }

    #endregion

    #region Token Info

    /// <summary>
    /// Token bilgilerini gösterir - Auth token ve refresh token durumu
    /// </summary>
    [HttpGet]
    public IActionResult TokenInfo()
    {
        var authToken = Request.Cookies[AuthTokenCookie];
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        var integrationTokens = Request.Cookies
            .Where(c => c.Key.StartsWith(IntegrationTokenPrefix))
            .Select(c => new
            {
                packageName = c.Key.Substring(IntegrationTokenPrefix.Length),
                tokenLength = c.Value?.Length ?? 0
            })
            .ToList();

        object? authTokenInfo = null;
        if (!string.IsNullOrEmpty(authToken))
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var expires = ParseUnixSeconds(User.FindFirst("exp")?.Value);
                authTokenInfo = new
                {
                    exists = true,
                    issuer = User.FindFirst("iss")?.Value,
                    audience = User.FindFirst("aud")?.Value,
                    issuedAt = ParseUnixSeconds(User.FindFirst("iat")?.Value),
                    expires,
                    isExpired = expires.HasValue && expires.Value < DateTime.UtcNow,
                    remainingMinutes = expires.HasValue ? (expires.Value - DateTime.UtcNow).TotalMinutes : (double?)null
                };
            }
            else
            {
                authTokenInfo = new { exists = true, valid = false, error = "Token gecersiz veya suresi dolmus" };
            }
        }

        return Json(new
        {
            authToken = authTokenInfo ?? new { exists = false },
            refreshToken = new
            {
                exists = !string.IsNullOrEmpty(refreshToken),
                length = refreshToken?.Length ?? 0
            },
            integrationTokens = integrationTokens,
            timestamp = DateTime.UtcNow
        });
    }

    #endregion

    #region Server Integration Tests

    /// <summary>
    /// Test Linbik.Server integration - Public endpoint (no auth required)
    /// Calls /api/serverTest/health through YARP proxy
    /// Expected: 200 OK with health status
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TestServerPublicEndpoint()
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            // Call through YARP proxy: /api/serverTest/health -> /api/integration/health
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var response = await client.GetAsync($"{baseUrl}/api/serverTest/health");
            var content = await response.Content.ReadAsStringAsync();

            return Json(new
            {
                success = response.IsSuccessStatusCode,
                test = "Server Public Endpoint (No Auth)",
                endpoint = "/api/serverTest/health -> /api/integration/health",
                statusCode = (int)response.StatusCode,
                expectedStatusCode = 200,
                passed = response.IsSuccessStatusCode,
                response = TryParseJson(content),
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                test = "Server Public Endpoint (No Auth)",
                error = ex.Message,
                hint = "Linbik.Server uygulamasının https://localhost:5481 adresinde çalıştığından emin olun",
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Test Linbik.Server integration - Public data endpoint
    /// Calls /api/serverTest/public-data through YARP proxy
    /// Expected: 200 OK with sample data
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TestServerPublicData()
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var response = await client.GetAsync($"{baseUrl}/api/serverTest/public-data");
            var content = await response.Content.ReadAsStringAsync();

            return Json(new
            {
                success = response.IsSuccessStatusCode,
                test = "Server Public Data Endpoint",
                endpoint = "/api/serverTest/public-data -> /api/integration/public-data",
                statusCode = (int)response.StatusCode,
                expectedStatusCode = 200,
                passed = response.IsSuccessStatusCode,
                response = TryParseJson(content),
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                test = "Server Public Data Endpoint",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Run all server integration tests and return summary
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> RunAllServerTests()
    {
        using var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var checks = new (string Name, string Path)[]
        {
            ("Public Health", "/api/serverTest/health"),
            ("Public Info", "/api/serverTest/info"),
            ("Public Data", "/api/serverTest/public-data"),
            ("Echo", "/api/serverTest/echo")
        };
        var results = new List<ServerCheckResult>();
        foreach (var (name, path) in checks)
            results.Add(await RunServerCheckAsync(httpClient, baseUrl, name, path, HttpContext.RequestAborted));

        var passedCount = results.Count(result => result.Passed);
        var totalCount = results.Count;

        return Json(new
        {
            summary = new
            {
                passed = passedCount,
                failed = totalCount - passedCount,
                total = totalCount,
                successRate = $"{(passedCount * 100.0 / totalCount):F1}%"
            },
            results,
            note = "Protected endpoint tests with auth require user to be logged in with integration access",
            timestamp = DateTime.UtcNow
        });
    }

    private static async Task<ServerCheckResult> RunServerCheckAsync(
        HttpClient client, string baseUrl, string name, string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(baseUrl + endpoint, cancellationToken);
            return new(name, response.IsSuccessStatusCode, endpoint, (int)response.StatusCode, 200);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new(name, false, Error: ex.Message);
        }
    }

    private static object? TryParseJson(string content)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<object>(content);
        }
        catch
        {
            return content;
        }
    }

    #endregion
}
