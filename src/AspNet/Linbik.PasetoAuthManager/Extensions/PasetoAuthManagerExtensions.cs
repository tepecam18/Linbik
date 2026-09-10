using Linbik.Core.Extensions;
using Linbik.Core.Responses;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.PasetoAuthManager.Configuration;
using Linbik.PasetoAuthManager.Models;
using Linbik.PasetoAuthManager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Linbik.Core.Services.LinbikAuthEndpointHelpers;

namespace Linbik.PasetoAuthManager.Extensions;

/// <summary>
/// Extension methods for Linbik PASETO authentication endpoints (login, callback, refresh, logout).
/// </summary>
public static class PasetoAuthManagerExtensions
{
    private const string AuthTokenCookie = Core.LinbikDefaults.AuthTokenCookie;
    private const string LinbikRefreshTokenCookie = Core.LinbikDefaults.RefreshTokenCookie;
    private const string UserNameCookie = Core.LinbikDefaults.UserNameCookie;
    private const string IntegrationTokenPrefix = Core.LinbikDefaults.IntegrationTokenPrefix;

    private static Task<string?> CreateLocalAccessTokenAsync(
        PasetoAuthOptions options,
        Core.Models.LinbikTokenResponse tokenResponse,
        DateTime accessTokenExpiry,
        IPasetoHelper pasetoHelper,
        ILogger logger)
        => LocalPasetoTokenIssuer.CreateAsync(options, tokenResponse, accessTokenExpiry, pasetoHelper, logger);

    private static IResult ReturnAuthSuccess(
        HttpContext context,
        Core.Configuration.LinbikClientConfig? clientConfig,
        string? redirectPath,
        LoginCallbackResponse data,
        string? successMessage = null)
    {
        if (IsMobileClient(clientConfig))
        {
            data.RedirectPath = redirectPath;
            return Results.Ok(new LBaseResponse<LoginCallbackResponse>(data));
        }

        if (!string.IsNullOrEmpty(redirectPath))
        {
            var redirectUrl = string.IsNullOrEmpty(successMessage)
                ? redirectPath
                : AppendMessageToUrl(redirectPath, successMessage, isError: false);
            return Results.Redirect(redirectUrl);
        }

        return Results.Ok(new LBaseResponse<LoginCallbackResponse>(data));
    }

    /// <summary>
    /// Map Linbik OAuth endpoints (login, callback, refresh, logout) backed by PASETO v4.public tokens.
    /// </summary>
    public static IEndpointRouteBuilder UseLinbikPasetoAuth(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<PasetoAuthOptions>>().Value;
        var linbikOptionsAccessor = endpoints.ServiceProvider.GetRequiredService<IOptions<Core.Configuration.LinbikOptions>>();
        var linbikOptions = linbikOptionsAccessor.Value;

        // Login - redirect to Linbik authorization
        endpoints.MapGet(options.LoginPath, async (HttpContext context,
            [FromServices] ILinbikAuthClient linbikClient,
            [FromQuery] string? name,
            [FromQuery] string? returnPath) =>
        {
            if (linbikOptions.KeylessMode)
            {
                var provisionClient = context.RequestServices.GetService<LinbikProvisionClient>();
                if (provisionClient != null)
                {
                    string appUrl = context.GetExternalScheme() + "://" + context.Request.Host.Value;
                    await provisionClient.EnsureProvisionedAsync(appUrl, options.LoginCallbackPath, name, context.RequestAborted);
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                if (linbikOptions.KeylessMode && linbikOptions.Clients.Count > 0)
                {
                    name = linbikOptions.Clients[0].Name;
                }
                else
                {
                    return Results.BadRequest(new LBaseResponse<object>("Keyless Mode is disabled or failed to provision. name is required."));
                }
            }

            var clientConfig = GetClientConfig(linbikOptions, name);
            if (clientConfig == null || string.IsNullOrEmpty(clientConfig.ClientId))
            {
                return Results.BadRequest(new LBaseResponse<object>($"Client ID not found for name: {name}."));
            }

            var initiateRequest = new Core.Models.LinbikInitiateRequest
            {
                ClientId = Guid.Parse(clientConfig.ClientId),
                ExtraData = new Dictionary<string, string>
                {
                    { "returnPath", returnPath ?? string.Empty }
                }
            };

            if (options.PkceEnabled)
            {
                var (verifier, challenge) = PkceService.Generate();
                PkceService.SaveVerifier(context.Response, verifier);
                initiateRequest.CodeChallenge = challenge;
            }

            var initiateResponse = await linbikClient.InitiateAuthAsync(initiateRequest, context.RequestAborted);
            if (!initiateResponse.IsSuccess || initiateResponse.Data is null)
            {
                var errorMessage = initiateResponse.FriendlyMessage?.Message ?? "Failed to initiate authorization flow.";
                return Results.BadRequest(new LBaseResponse<object>(
                    initiateResponse.FriendlyMessage?.Title ?? "initiate_failed", errorMessage));
            }

            if (IsMobileClient(clientConfig))
            {
                return Results.Ok(new LBaseResponse<LoginResponse>(new LoginResponse
                {
                    RedirectPath = initiateResponse.Data.RedirectUrl
                }));
            }

            return Results.Redirect(initiateResponse.Data.RedirectUrl);
        }).WithTags("Linbik").AllowAnonymous().RequireRateLimiting(LinbikRateLimitingExtensions.LinbikAuthPolicy);

        // Login callback
        endpoints.MapGet(options.LoginCallbackPath, async (HttpContext context,
            [FromServices] ILinbikAuthClient linbikClient,
            [FromServices] IPasetoHelper pasetoHelper,
            [FromServices] ILogger<ILinbikAuthClient> logger,
            [FromServices] IAuditLogger auditLogger,
            [FromServices] LinbikMetrics metrics) =>
        {
            var timer = metrics.StartTimer();
            string? userId = null;
            Core.Configuration.LinbikClientConfig? clientConfig = null;
            string? redirectPath = null;

            try
            {
                var code = context.Request.Query["code"].FirstOrDefault();
                if (string.IsNullOrEmpty(code))
                {
                    await auditLogger.LogAsync(AuditEventType.TokenExchangeFailed, null, "Authorization code is required", false);
                    metrics.RecordTokenExchange(false, timer.ElapsedSeconds);
                    return ReturnAuthError(clientConfig, redirectPath, "Authorization code is required");
                }

                if (linbikOptions.KeylessMode && (string.IsNullOrEmpty(linbikOptions.ServiceId) || string.IsNullOrEmpty(linbikOptions.ApiKey)))
                {
                    logger.LogWarning("Login callback received before Keyless Mode provisioning completed.");
                    await auditLogger.LogAsync(AuditEventType.TokenExchangeFailed, null, "Keyless Mode provisioning not complete", false);
                    metrics.RecordTokenExchange(false, timer.ElapsedSeconds);
                    return ReturnAuthError(clientConfig, redirectPath, "Service is still initializing. Please try again in a moment.");
                }

                var tokenResponse = await linbikClient.ExchangeCodeAsync(code);
                if (tokenResponse is null)
                {
                    await auditLogger.LogAsync(AuditEventType.TokenExchangeFailed, null, "Token exchange failed", false);
                    metrics.RecordTokenExchange(false, timer.ElapsedSeconds);
                    return ReturnAuthError(clientConfig, redirectPath, "Token exchange failed");
                }

                userId = tokenResponse.UserId.ToString();

                if (tokenResponse.ClientId.HasValue)
                {
                    var clientIdStr = tokenResponse.ClientId.Value.ToString();
                    clientConfig = linbikOptions.Clients.FirstOrDefault(c =>
                        string.Equals(c.ClientId, clientIdStr, StringComparison.OrdinalIgnoreCase));
                }

                if (tokenResponse.ExtraData != null && tokenResponse.ExtraData.returnPath != null)
                {
                    var pathFromQuery = tokenResponse.ExtraData.returnPath;
                    if (!string.IsNullOrEmpty(pathFromQuery) && clientConfig != null)
                    {
                        redirectPath = clientConfig.RedirectUrl.TrimEnd('/') + "/" + pathFromQuery.TrimStart('/');
                    }
                    else if (clientConfig != null)
                    {
                        redirectPath = clientConfig.RedirectUrl.TrimEnd('/');
                    }
                }
                else if (clientConfig != null)
                {
                    redirectPath = clientConfig.RedirectUrl;
                }

                if (string.IsNullOrEmpty(redirectPath))
                    redirectPath = "/";

                if (options.PkceEnabled)
                {
                    if (string.IsNullOrEmpty(tokenResponse.CodeChallenge))
                    {
                        logger.LogWarning("PKCE enabled but CodeChallenge missing in token response for user {UserId}", tokenResponse.UserId);
                        await auditLogger.LogAsync(AuditEventType.PkceValidationFailed, userId, "CodeChallenge missing in token response", false);
                        metrics.RecordLoginFailure("pkce_failed");
                        return ReturnAuthError(clientConfig, redirectPath, "PKCE verification failed");
                    }

                    var verifier = PkceService.GetVerifier(context.Request);
                    if (!string.IsNullOrEmpty(verifier))
                    {
                        if (!PkceService.VerifyChallengeMatches(verifier, tokenResponse.CodeChallenge))
                        {
                            logger.LogWarning("PKCE verification failed for user {UserId}", tokenResponse.UserId);
                            await auditLogger.LogAsync(AuditEventType.PkceValidationFailed, userId, "PKCE verification failed", false);
                            metrics.RecordLoginFailure("pkce_failed");
                            return ReturnAuthError(clientConfig, redirectPath, "PKCE verification failed");
                        }
                        PkceService.DeleteVerifier(context.Response);
                    }
                }

                var accessTokenExpiry = CalculateExpiry(
                    tokenResponse.AccessTokenExpiresAt,
                    DateTime.UtcNow.AddMinutes(options.AccessTokenExpirationMinutes));

                var refreshTokenExpiry = CalculateExpiry(
                    tokenResponse.RefreshTokenExpiresAt,
                    DateTime.UtcNow.AddDays(options.RefreshTokenExpirationDays));

                var accessToken = await CreateLocalAccessTokenAsync(options, tokenResponse, accessTokenExpiry, pasetoHelper, logger);
                if (accessToken is null)
                {
                    return ReturnAuthError(clientConfig, redirectPath, "Authentication is not properly configured");
                }

                await context.RequestServices.GetRequiredService<LinbikRefreshTokenManager>().EnsureAsync(tokenResponse, refreshTokenExpiry, context.RequestAborted);

                SetAuthCookies(context, tokenResponse, accessToken, accessTokenExpiry, refreshTokenExpiry,
                    !string.IsNullOrEmpty(linbikOptions.CookieDomain) ? linbikOptions.CookieDomain : context.Request.Host.Host,
                    linbikOptions.SameSite);

                timer.Stop();
                await auditLogger.LogTokenExchangeAsync(userId, linbikOptions.ServiceId, true, timer.ElapsedMilliseconds);
                metrics.RecordTokenExchange(true, timer.ElapsedSeconds, linbikOptions.ServiceId);
                metrics.RecordLoginSuccess(tokenResponse.ClientId?.ToString() ?? linbikOptions.Clients.FirstOrDefault()?.ClientId);

                var responseData = new LoginCallbackResponse
                {
                    UserId = tokenResponse.UserId,
                    UserName = tokenResponse.Username,
                    DisplayName = tokenResponse.DisplayName ?? tokenResponse.Username,
                    Integrations = tokenResponse.Integrations?.Select(i => i.PackageName).ToList() ?? []
                };
                return ReturnAuthSuccess(context, clientConfig, redirectPath, responseData);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login callback failed");
                await auditLogger.LogAsync(AuditEventType.TokenExchangeFailed, userId, ex.Message, false);
                metrics.RecordTokenExchange(false, timer.ElapsedSeconds);
                return ReturnAuthError(clientConfig, redirectPath, "Login failed. Please try again.");
            }
        }).WithTags("Linbik").AllowAnonymous().RequireRateLimiting("LinbikStrict");

        // Logout
        endpoints.MapGet(options.LogoutPath, async (HttpContext context,
            [FromServices] ILocalPasetoTokenReader localTokenReader,
            [FromServices] IAuditLogger auditLogger) =>
        {
            await context.RequestServices.GetRequiredService<LinbikRefreshTokenManager>().RevokeAsync(context.Request.Cookies[LinbikRefreshTokenCookie], context.RequestAborted);
            var deleteCookieOptions = new CookieOptions { Path = "/", Domain = linbikOptions.CookieDomain, SameSite = linbikOptions.SameSite };

            // Get user ID from PASETO cookie (no signature validation — best-effort for audit).
            // Uses the mode-aware local reader; never mixes with Linbik API application tokens.
            var authToken = context.Request.Cookies[AuthTokenCookie];
            string? userId = null;
            if (!string.IsNullOrEmpty(authToken))
            {
                var claims = localTokenReader.Read(authToken);
                userId = claims.GetValueOrDefault("sub");
            }

            context.Response.Cookies.Delete(AuthTokenCookie, deleteCookieOptions);
            context.Response.Cookies.Delete(LinbikRefreshTokenCookie, deleteCookieOptions);
            context.Response.Cookies.Delete(UserNameCookie, deleteCookieOptions);

            foreach (var cookie in context.Request.Cookies)
            {
                if (cookie.Key.StartsWith(IntegrationTokenPrefix))
                {
                    context.Response.Cookies.Delete(cookie.Key, deleteCookieOptions);
                }
            }

            await auditLogger.LogAsync(AuditEventType.LogoutSuccess, userId, "User logged out successfully");
            return Results.Ok(new LBaseResponse<object>(isSuccess: true));
        }).WithTags("Linbik").RequireRateLimiting(LinbikRateLimitingExtensions.LinbikAuthPolicy);

        // Refresh
        endpoints.MapPost(options.RefreshPath, async (HttpContext context,
            [FromServices] ILinbikAuthClient linbikClient,
            [FromServices] IPasetoHelper pasetoHelper,
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

                var tokenResponse = await context.RequestServices.GetRequiredService<LinbikRefreshTokenManager>().RefreshAsync(refreshToken, linbikClient, context.RequestAborted);
                if (tokenResponse is null)
                {
                    await auditLogger.LogAsync(AuditEventType.TokenRefreshFailed, null, "Token refresh returned null", false);
                    metrics.RecordTokenRefresh(false, timer.ElapsedSeconds);
                    return Results.Unauthorized();
                }

                userId = tokenResponse.UserId.ToString();

                var accessTokenExpiry = CalculateExpiry(
                    tokenResponse.AccessTokenExpiresAt,
                    DateTime.UtcNow.AddMinutes(options.AccessTokenExpirationMinutes));

                var refreshTokenExpiry = CalculateExpiry(
                    tokenResponse.RefreshTokenExpiresAt,
                    DateTime.UtcNow.AddDays(options.RefreshTokenExpirationDays));

                var accessToken = await CreateLocalAccessTokenAsync(options, tokenResponse, accessTokenExpiry, pasetoHelper, logger);
                if (accessToken is null)
                {
                    return Results.BadRequest(new LBaseResponse<object>("Authentication is not properly configured"));
                }

                SetAuthCookies(context, tokenResponse, accessToken, accessTokenExpiry, refreshTokenExpiry,
                    !string.IsNullOrEmpty(linbikOptions.CookieDomain) ? linbikOptions.CookieDomain : context.Request.Host.Host,
                    linbikOptions.SameSite);

                timer.Stop();
                await auditLogger.LogTokenRefreshAsync(userId, linbikOptions.ServiceId, true, timer.ElapsedMilliseconds);
                metrics.RecordTokenRefresh(true, timer.ElapsedSeconds, linbikOptions.ServiceId);

                return Results.Ok(new LBaseResponse<object>(new
                {
                    userId = tokenResponse.UserId,
                    userName = tokenResponse.Username,
                    displayName = tokenResponse.DisplayName,
                    integrations = tokenResponse.Integrations?.Select(i => i.PackageName).ToList() ?? []
                }));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Token refresh failed");
                await auditLogger.LogAsync(AuditEventType.TokenRefreshFailed, userId, ex.Message, false);
                metrics.RecordTokenRefresh(false, timer.ElapsedSeconds);
                return Results.BadRequest(new LBaseResponse<object>("Token refresh failed"));
            }
        }).WithTags("Linbik").RequireRateLimiting("LinbikStrict");

        return endpoints;
    }

    /// <summary>
    /// Get integration token from cookie.
    /// </summary>
    public static string? GetIntegrationToken(this HttpContext context, string packageName)
    {
        var cookieName = $"{IntegrationTokenPrefix}{packageName}";
        return context.Request.Cookies[cookieName];
    }

    /// <summary>
    /// Check if user has integration tokens.
    /// </summary>
    public static bool HasIntegrations(this HttpContext context)
    {
        return context.Request.Cookies.Any(c => c.Key.StartsWith(IntegrationTokenPrefix));
    }
}
