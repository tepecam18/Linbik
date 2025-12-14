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
    /// Check if the request accepts HTML (browser request)
    /// </summary>
    private static bool AcceptsHtml(HttpContext context)
    {
        var accept = context.Request.Headers.Accept.ToString();
        return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Return appropriate response based on Accept header
    /// For HTML: Redirect to error page
    /// For JSON/API: Return LBaseResponse
    /// </summary>
    private static IResult ReturnError(HttpContext context, JwtAuthOptions options, string errorMessage, int statusCode = 400)
    {
        if (AcceptsHtml(context))
        {
            var redirectUrl = options.ErrorRedirectUrl.Replace("{error}", Uri.EscapeDataString(errorMessage));
            return Results.Redirect(redirectUrl);
        }

        return statusCode switch
        {
            401 => Results.Unauthorized(),
            403 => Results.Forbid(),
            _ => Results.BadRequest(new LBaseResponse<object>(errorMessage))
        };
    }

    /// <summary>
    /// Return appropriate success response based on Accept header
    /// For HTML: Redirect to success page
    /// For JSON/API: Return LBaseResponse with data
    /// </summary>
    private static IResult ReturnSuccess<T>(HttpContext context, string redirectUrl, T? data = default) where T : class
    {
        if (AcceptsHtml(context))
        {
            return Results.Redirect(redirectUrl);
        }

        return data is not null
            ? Results.Ok(new LBaseResponse<T>(data))
            : Results.Ok(new LBaseResponse<object>(isSuccess: true));
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
                    return ReturnError(context, options, "Authorization code is required");
                }

                // Exchange code for tokens
                var tokenResponse = await linbikClient.ExchangeCodeAsync(code);
                if (tokenResponse is null)
                {
                    await auditLogger.LogAsync(AuditEventType.TokenExchangeFailed, null, "Token exchange failed", false);
                    metrics.RecordTokenExchange(false, timer.ElapsedSeconds);
                    return ReturnError(context, options, "Token exchange failed");
                }

                userId = tokenResponse.UserId.ToString();

                // PKCE verification (client-side)
                if (options.PkceEnabled)
                {
                    if(string.IsNullOrEmpty(tokenResponse.CodeChallenge))
                    {
                        logger.LogWarning("PKCE is enabled but CodeChallenge is missing in token response for user {UserId}", tokenResponse.UserId);
                        await auditLogger.LogAsync(AuditEventType.PkceValidationFailed, userId, "CodeChallenge missing in token response", false);
                        metrics.RecordLoginFailure("pkce_failed");
                        return ReturnError(context, options, "PKCE verification failed");
                    }

                    var verifier = PkceService.GetVerifier(context.Request);
                    if (!string.IsNullOrEmpty(verifier))
                    {
                        if (!PkceService.VerifyChallengeMatches(verifier, tokenResponse.CodeChallenge))
                        {
                            logger.LogWarning("PKCE verification failed for user {UserId}", tokenResponse.UserId);
                            await auditLogger.LogAsync(AuditEventType.PkceValidationFailed, userId, "PKCE verification failed", false);
                            metrics.RecordLoginFailure("pkce_failed");
                            return ReturnError(context, options, "PKCE verification failed");
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
                    return ReturnError(context, options, "Authentication is not properly configured");
                }

                if (options.SecretKey.Length < MinSecretKeyLength)
                {
                    logger.LogError("SecretKey is too short. Minimum length is {MinLength} characters for HS256. Current length: {CurrentLength}", MinSecretKeyLength, options.SecretKey.Length);
                    return ReturnError(context, options, "Authentication is not properly configured");
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

                // Log successful login
                timer.Stop();
                await auditLogger.LogTokenExchangeAsync(userId, linbikOptions.ServiceId, true, timer.ElapsedMilliseconds);
                metrics.RecordTokenExchange(true, timer.ElapsedSeconds, linbikOptions.ServiceId);
                metrics.RecordLoginSuccess(linbikOptions.ClientId);

                // Return success based on Accept header
                return ReturnSuccess(context, options.LoginRedirectUrl, new
                {
                    userId = tokenResponse.UserId,
                    userName = tokenResponse.Username,
                    displayName = tokenResponse.DisplayName,
                    integrations = tokenResponse.Integrations?.Select(i => i.PackageName).ToList() ?? new List<string>()
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login callback failed");
                await auditLogger.LogAsync(AuditEventType.TokenExchangeFailed, userId, ex.Message, false);
                metrics.RecordTokenExchange(false, timer.ElapsedSeconds);
                return ReturnError(context, options, "Login failed. Please try again.");
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
            return ReturnSuccess<object>(context, options.LogoutRedirectUrl, null);
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
                    return ReturnError(context, options, "No refresh token provided", 401);
                }

                var tokenResponse = await linbikClient.RefreshTokensAsync(refreshToken);
                if (tokenResponse is null)
                {
                    await auditLogger.LogAsync(AuditEventType.TokenRefreshFailed, null, "Token refresh returned null", false);
                    metrics.RecordTokenRefresh(false, timer.ElapsedSeconds);
                    return ReturnError(context, options, "Token refresh failed", 401);
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
                    return ReturnError(context, options, "Authentication is not properly configured");
                }

                if (options.SecretKey.Length < MinSecretKeyLength)
                {
                    logger.LogError("SecretKey is too short. Minimum length is {MinLength} characters", MinSecretKeyLength);
                    return ReturnError(context, options, "Authentication is not properly configured");
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

                // Refresh always returns JSON (typically called by API clients)
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
                return ReturnError(context, options, "Token refresh failed");
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

