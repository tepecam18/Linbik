using Linbik.Core.Interfaces;
using Linbik.Core.Responses;
using Linbik.Core.Services;
using Linbik.JwtAuthManager.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Linbik.JwtAuthManager.Extensions;

/// <summary>
/// Extension methods for Linbik JWT authentication endpoints
/// </summary>
public static class JwtAuthManagerExtensions
{
    private const string AuthTokenCookie = "authToken";
    private const string RefreshTokenCookie = "refreshToken";
    private const string LinbikRefreshTokenCookie = "linbikRefreshToken";
    private const string UserNameCookie = "userName";
    private const string IntegrationTokenPrefix = "integration_";
    private const int MinSecretKeyLength = 32; // 256-bit minimum for HS256

    /// <summary>
    /// Validates that a URL is a safe local URL to prevent Open Redirect attacks
    /// </summary>
    private static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        // Only allow relative URLs starting with / but not // (protocol-relative)
        if (url.StartsWith("/") && !url.StartsWith("//") && !url.StartsWith("/\\"))
            return true;

        // Also allow ~ for ASP.NET virtual paths
        if (url.StartsWith("~/"))
            return true;

        return false;
    }

    /// <summary>
    /// Map Linbik OAuth endpoints (login, callback, refresh, logout)
    /// </summary>
    public static IEndpointRouteBuilder MapLinbikEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<JwtAuthOptions>>().Value;
        var linbikOptions = endpoints.ServiceProvider.GetRequiredService<IOptions<Linbik.Core.Configuration.LinbikOptions>>().Value;

        // Login - redirect to Linbik authorization
        endpoints.MapGet(options.LoginPath, (HttpContext context,
            [FromQuery] string? returnUrl) =>
        {
            // Build authorization URL: LinbikUrl + /auth + ServiceId + CodeChallenge
            var baseUrl = linbikOptions.LinbikUrl.TrimEnd('/');
            var authEndpoint = linbikOptions.AuthorizationEndpoint.TrimStart('/');
            var clientId = linbikOptions.ClientId;

            string authorizationUrl;

            // Generate PKCE code challenge if enabled
            if (options.PkceEnabled)
            {
                var (verifier, challenge) = PkceService.Generate();
                PkceService.SaveVerifier(context.Response, verifier);
                authorizationUrl = $"{baseUrl}/{authEndpoint}/{clientId}/{challenge}";
            }
            else
            {
                authorizationUrl = $"{baseUrl}/{authEndpoint}/{clientId}";
            }

            // Store return URL in cookie for callback (only if it's a safe local URL)
            if (IsLocalUrl(returnUrl))
            {
                context.Response.Cookies.Append("linbik_return_url", returnUrl!, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromMinutes(10),
                    Path = "/"
                });
            }

            return Results.Redirect(authorizationUrl);
        }).WithTags("Linbik.Auth").AllowAnonymous().RequireRateLimiting(RateLimitExtensions.LinbikAuthPolicy);

        // Login callback - exchange authorization code for tokens
        endpoints.MapGet(options.LoginCallbackPath, async (HttpContext context,
            [FromServices] ILinbikAuthClient linbikClient,
            [FromServices] ILogger<ILinbikAuthClient> logger,
            [FromServices] IAuditLogger auditLogger,
            [FromServices] LinbikMetrics metrics) =>
        {
            var timer = metrics.StartTimer();
            string? userId = null;

            try
            {
                var code = context.Request.Query["code"].FirstOrDefault();
                if (string.IsNullOrEmpty(code))
                {
                    await auditLogger.LogAsync(AuditEventType.TokenExchangeFailed, null, "Authorization code is required", false);
                    metrics.RecordTokenExchange(false, timer.ElapsedSeconds);
                    return Results.BadRequest(new LBaseResponse<object>("Authorization code is required"));
                }

                // Exchange code for tokens
                var tokenResponse = await linbikClient.ExchangeCodeAsync(code);
                if (tokenResponse is null)
                {
                    await auditLogger.LogAsync(AuditEventType.TokenExchangeFailed, null, "Token exchange failed", false);
                    metrics.RecordTokenExchange(false, timer.ElapsedSeconds);
                    return Results.BadRequest(new LBaseResponse<object>("Token exchange failed"));
                }

                userId = tokenResponse.UserId.ToString();

                // PKCE verification (client-side)
                if (options.PkceEnabled && !string.IsNullOrEmpty(tokenResponse.CodeChallenge))
                {
                    var verifier = PkceService.GetVerifier(context.Request);
                    if (!string.IsNullOrEmpty(verifier))
                    {
                        if (!PkceService.VerifyChallengeMatches(verifier, tokenResponse.CodeChallenge))
                        {
                            logger.LogWarning("PKCE verification failed for user {UserId}", tokenResponse.UserId);
                            await auditLogger.LogAsync(AuditEventType.PkceValidationFailed, userId, "PKCE verification failed", false);
                            metrics.RecordLoginFailure("pkce_failed");
                            return Results.BadRequest(new LBaseResponse<object>("PKCE verification failed"));
                        }
                        PkceService.DeleteVerifier(context.Response);
                    }
                }

                // Calculate expiry (check > 0 to avoid 1970 epoch date when value is 0)
                var accessTokenExpiry = tokenResponse.AccessTokenExpiresAt.HasValue && tokenResponse.AccessTokenExpiresAt.Value > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.AccessTokenExpiresAt.Value).UtcDateTime
                    : DateTime.UtcNow.AddMinutes(options.AccessTokenExpirationMinutes);

                var refreshTokenExpiry = tokenResponse.RefreshTokenExpiresAt.HasValue && tokenResponse.RefreshTokenExpiresAt.Value > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.RefreshTokenExpiresAt.Value).UtcDateTime
                    : DateTime.UtcNow.AddDays(options.RefreshTokenExpirationDays);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None
                };

                // Store refresh token
                if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    context.Response.Cookies.Append(LinbikRefreshTokenCookie, tokenResponse.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = refreshTokenExpiry,
                        Path = "/"
                    });
                }

                // Store integration tokens
                if (tokenResponse.Integrations?.Count > 0)
                {
                    foreach (var integration in tokenResponse.Integrations)
                    {
                        var integrationCookieName = $"{IntegrationTokenPrefix}{integration.PackageName}";
                        context.Response.Cookies.Append(integrationCookieName, integration.Token, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.None,
                            Expires = accessTokenExpiry,
                            Path = "/"
                        });
                    }
                }

                // Create local access token (for cookie auth)
                if (string.IsNullOrEmpty(options.SecretKey))
                {
                    logger.LogError("SecretKey is not configured in JwtAuthOptions. User authentication will not work. Please set 'Linbik:JwtAuth:SecretKey' in appsettings.json");
                    return Results.Problem("Authentication is not properly configured. SecretKey is missing.");
                }

                if (options.SecretKey.Length < MinSecretKeyLength)
                {
                    logger.LogError("SecretKey is too short. Minimum length is {MinLength} characters for HS256. Current length: {CurrentLength}", MinSecretKeyLength, options.SecretKey.Length);
                    return Results.Problem("Authentication is not properly configured. SecretKey is too weak.");
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, tokenResponse.UserId.ToString()),
                    new(ClaimTypes.Name, tokenResponse.Username),
                    new("display_name", tokenResponse.DisplayName ?? tokenResponse.Username)
                };

                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey));
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: options.JwtIssuer,
                    audience: options.JwtAudience,
                    claims: claims,
                    expires: accessTokenExpiry,
                    signingCredentials: credentials
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                context.Response.Cookies.Append(AuthTokenCookie, tokenString, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = accessTokenExpiry,
                    Path = "/"
                });

                context.Response.Cookies.Append(UserNameCookie, tokenResponse.Username, new CookieOptions
                {
                    HttpOnly = false,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = refreshTokenExpiry,
                    Path = "/"
                });

                // Check for return URL cookie and redirect (with Open Redirect protection)
                var returnUrl = context.Request.Cookies["linbik_return_url"];
                context.Response.Cookies.Delete("linbik_return_url", new CookieOptions { Path = "/" });

                // Log successful login
                timer.Stop();
                await auditLogger.LogTokenExchangeAsync(userId, linbikOptions.ServiceId, true, timer.ElapsedMilliseconds);
                metrics.RecordTokenExchange(true, timer.ElapsedSeconds, linbikOptions.ServiceId);
                metrics.RecordLoginSuccess(linbikOptions.ClientId);

                if (IsLocalUrl(returnUrl))
                {
                    return Results.Redirect(returnUrl!);
                }

                // Default redirect to home page
                return Results.Redirect("/");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login callback failed");
                await auditLogger.LogAsync(AuditEventType.TokenExchangeFailed, userId, ex.Message, false);
                metrics.RecordTokenExchange(false, timer.ElapsedSeconds);
                return Results.Problem(ex.Message);
            }
        }).WithTags("Linbik.Auth").AllowAnonymous().RequireRateLimiting("LinbikStrict");

        // Logout endpoint (GET for simple link usage)
        endpoints.MapGet(options.LogoutPath, async (HttpContext context,
            [FromServices] IAuditLogger auditLogger) =>
        {
            var deleteCookieOptions = new CookieOptions { Path = "/" };

            // Get user ID before deleting cookies
            var authToken = context.Request.Cookies[AuthTokenCookie];
            string? userId = null;
            if (!string.IsNullOrEmpty(authToken))
            {
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwt = handler.ReadJwtToken(authToken);
                    userId = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
                }
                catch { /* Ignore token parsing errors */ }
            }

            // Delete all auth cookies
            context.Response.Cookies.Delete(AuthTokenCookie, deleteCookieOptions);
            context.Response.Cookies.Delete(RefreshTokenCookie, deleteCookieOptions);
            context.Response.Cookies.Delete(LinbikRefreshTokenCookie, deleteCookieOptions);
            context.Response.Cookies.Delete(UserNameCookie, deleteCookieOptions);

            // Delete integration cookies
            foreach (var cookie in context.Request.Cookies)
            {
                if (cookie.Key.StartsWith(IntegrationTokenPrefix))
                {
                    context.Response.Cookies.Delete(cookie.Key, deleteCookieOptions);
                }
            }

            await auditLogger.LogAsync(AuditEventType.LogoutSuccess, userId, "User logged out successfully");
            return Results.Ok(new LBaseResponse<object>("Logged out successfully"));
        }).WithTags("Linbik.Auth").RequireRateLimiting(RateLimitExtensions.LinbikAuthPolicy);

        // Refresh endpoint
        endpoints.MapPost(options.RefreshPath, async (HttpContext context,
            [FromServices] ILinbikAuthClient linbikClient,
            [FromServices] ILogger<ILinbikAuthClient> logger,
            [FromServices] IAuditLogger auditLogger,
            [FromServices] LinbikMetrics metrics) =>
        {
            var timer = metrics.StartTimer();
            string? userId = null;

            try
            {
                var refreshToken = context.Request.Cookies[LinbikRefreshTokenCookie];
                if (string.IsNullOrEmpty(refreshToken))
                {
                    await auditLogger.LogAsync(AuditEventType.TokenRefreshFailed, null, "No refresh token provided", false);
                    metrics.RecordTokenRefresh(false, timer.ElapsedSeconds);
                    return Results.Unauthorized();
                }

                var tokenResponse = await linbikClient.RefreshTokensAsync(refreshToken);
                if (tokenResponse is null)
                {
                    await auditLogger.LogAsync(AuditEventType.TokenRefreshFailed, null, "Token refresh returned null", false);
                    metrics.RecordTokenRefresh(false, timer.ElapsedSeconds);
                    return Results.Unauthorized();
                }

                userId = tokenResponse.UserId.ToString();

                // Calculate expiry (check > 0 to avoid 1970 epoch date when value is 0)
                var accessTokenExpiry = tokenResponse.AccessTokenExpiresAt.HasValue && tokenResponse.AccessTokenExpiresAt.Value > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.AccessTokenExpiresAt.Value).UtcDateTime
                    : DateTime.UtcNow.AddMinutes(options.AccessTokenExpirationMinutes);

                var refreshTokenExpiry = tokenResponse.RefreshTokenExpiresAt.HasValue && tokenResponse.RefreshTokenExpiresAt.Value > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.RefreshTokenExpiresAt.Value).UtcDateTime
                    : DateTime.UtcNow.AddDays(options.RefreshTokenExpirationDays);

                // Update refresh token
                if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    context.Response.Cookies.Append(LinbikRefreshTokenCookie, tokenResponse.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = refreshTokenExpiry,
                        Path = "/"
                    });
                }

                // Update integration tokens
                if (tokenResponse.Integrations?.Count > 0)
                {
                    foreach (var integration in tokenResponse.Integrations)
                    {
                        var integrationCookieName = $"{IntegrationTokenPrefix}{integration.PackageName}";
                        context.Response.Cookies.Append(integrationCookieName, integration.Token, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.None,
                            Expires = accessTokenExpiry,
                            Path = "/"
                        });
                    }
                }

                // Update local access token
                if (string.IsNullOrEmpty(options.SecretKey))
                {
                    logger.LogError("SecretKey is not configured in JwtAuthOptions");
                    return Results.Problem("Authentication is not properly configured");
                }

                if (options.SecretKey.Length < MinSecretKeyLength)
                {
                    logger.LogError("SecretKey is too short. Minimum length is {MinLength} characters", MinSecretKeyLength);
                    return Results.Problem("Authentication is not properly configured");
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, tokenResponse.UserId.ToString()),
                    new(ClaimTypes.Name, tokenResponse.Username),
                    new("display_name", tokenResponse.DisplayName ?? tokenResponse.Username)
                };

                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey));
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: options.JwtIssuer,
                    audience: options.JwtAudience,
                    claims: claims,
                    expires: accessTokenExpiry,
                    signingCredentials: credentials
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                context.Response.Cookies.Append(AuthTokenCookie, tokenString, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = accessTokenExpiry,
                    Path = "/"
                });

                // Log successful refresh
                timer.Stop();
                await auditLogger.LogTokenRefreshAsync(userId, linbikOptions.ServiceId, true, timer.ElapsedMilliseconds);
                metrics.RecordTokenRefresh(true, timer.ElapsedSeconds, linbikOptions.ServiceId);

                return Results.Ok(new LBaseResponse<object>(new
                {
                    userId = tokenResponse.UserId,
                    userName = tokenResponse.Username,
                    displayName = tokenResponse.DisplayName,
                    integrations = tokenResponse.Integrations?.Select(i => i.PackageName).ToList() ?? new List<string>()
                }));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Token refresh failed");
                await auditLogger.LogAsync(AuditEventType.TokenRefreshFailed, userId, ex.Message, false);
                metrics.RecordTokenRefresh(false, timer.ElapsedSeconds);
                return Results.Problem(ex.Message);
            }
        }).WithTags("Linbik.Auth").RequireRateLimiting("LinbikStrict");

        return endpoints;
    }

    /// <summary>
    /// Get integration token from cookie
    /// </summary>
    public static string? GetIntegrationToken(this HttpContext context, string packageName)
    {
        var cookieName = $"{IntegrationTokenPrefix}{packageName}";
        return context.Request.Cookies[cookieName];
    }

    /// <summary>
    /// Check if user has integration tokens
    /// </summary>
    public static bool HasIntegrations(this HttpContext context)
    {
        return context.Request.Cookies.Any(c => c.Key.StartsWith(IntegrationTokenPrefix));
    }
}

