using AspNet.Models;
using Linbik.Core.Interfaces;
using Linbik.Core.Models;
using Linbik.Core.Services;
using Linbik.JwtAuthManager.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AspNet.Controllers;

public class TestController : Controller
{
    private const string AuthTokenCookie = "authToken";
    private const string RefreshTokenCookie = "linbikRefreshToken";
    private const string IntegrationTokenPrefix = "integration_";

    private readonly IAuditLogger _auditLogger;
    private readonly LinbikMetrics _metrics;
    private readonly IAuthService _authService;

    public TestController(IAuditLogger auditLogger, LinbikMetrics metrics, IAuthService authService)
    {
        _auditLogger = auditLogger;
        _metrics = metrics;
        _authService = authService;
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

    #region Authentication Tests

    /// <summary>
    /// Protected endpoint - Requires valid Linbik JWT token in cookie
    /// Uses [LinbikAuthorize] attribute for authentication
    /// </summary>
    [LinbikAuthorize]
    [HttpGet]
    public IActionResult Protected()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = User.FindFirst(ClaimTypes.Name)?.Value;
        var displayName = User.FindFirst("display_name")?.Value;

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
            userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            userName = User.FindFirst(ClaimTypes.Name)?.Value,
            displayName = User.FindFirst("display_name")?.Value,
            email = User.FindFirst(ClaimTypes.Email)?.Value,
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
    public async Task<IActionResult> TestRefreshToken()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Json(new
            {
                success = false,
                error = "Refresh token bulunamadı",
                message = "Önce giriş yapmanız gerekiyor. Refresh token cookie'de saklanır.",
                cookieName = RefreshTokenCookie
            });
        }

        try
        {
            var result = await _authService.RefreshTokensAsync(HttpContext);

            if (result)
            {
                return Json(new
                {
                    success = true,
                    message = "✅ Token'lar başarıyla yenilendi!",
                    newAuthToken = !string.IsNullOrEmpty(Request.Cookies[AuthTokenCookie]),
                    timestamp = DateTime.UtcNow
                });
            }
            else
            {
                return Json(new
                {
                    success = false,
                    error = "Token yenileme başarısız",
                    message = "Linbik sunucusu refresh isteğini reddetti. Token süresi dolmuş olabilir.",
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                error = "Token yenileme hatası",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
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
        _metrics.RecordLoginAttempt("rate-test");

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
        _metrics.RecordLoginAttempt("strict-test");

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
    /// Mock Integration Service endpoint - Tests [LinbikIntegrationAuthorize]
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
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            // Token bilgilerini göster (validation yapmadan - sadece demo)
            return Json(new
            {
                success = true,
                message = "✅ Integration service mock endpoint'ine erişildi!",
                note = "Gerçek integration service RSA public key ile doğrulama yapar",
                tokenInfo = new
                {
                    issuer = jwt.Issuer,
                    audience = jwt.Audiences.FirstOrDefault(),
                    subject = jwt.Subject,
                    issuedAt = jwt.IssuedAt,
                    expires = jwt.ValidTo,
                    claims = jwt.Claims.Select(c => new { c.Type, c.Value })
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                error = "Geçersiz JWT token",
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
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            return Json(new
            {
                success = true,
                packageName = packageName,
                message = "✅ Integration token bulundu ve decode edildi!",
                note = "Bu token, integration service'e istek yaparken Authorization header'a eklenir",
                tokenInfo = new
                {
                    issuer = jwt.Issuer,
                    audience = jwt.Audiences.FirstOrDefault(),
                    subject = jwt.Subject,
                    issuedAt = jwt.IssuedAt,
                    expires = jwt.ValidTo,
                    isExpired = jwt.ValidTo < DateTime.UtcNow,
                    claimCount = jwt.Claims.Count()
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
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(authToken);
                authTokenInfo = new
                {
                    exists = true,
                    issuer = jwt.Issuer,
                    audience = jwt.Audiences.FirstOrDefault(),
                    issuedAt = jwt.IssuedAt,
                    expires = jwt.ValidTo,
                    isExpired = jwt.ValidTo < DateTime.UtcNow,
                    remainingMinutes = (jwt.ValidTo - DateTime.UtcNow).TotalMinutes
                };
            }
            catch
            {
                authTokenInfo = new { exists = true, valid = false, error = "Token decode edilemedi" };
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
}

public class AuditTestRequest
{
    public string? EventType { get; set; }
    public string? Message { get; set; }
    public object? TestData { get; set; }
}
