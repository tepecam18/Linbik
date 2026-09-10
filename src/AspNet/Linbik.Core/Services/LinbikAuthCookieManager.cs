using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Linbik.Core.Services;

/// <summary>
/// Encapsulates the cookie-writing/clearing logic used by <see cref="LinbikAuthService"/>.
/// </summary>
internal sealed class LinbikAuthCookieManager(ILogger logger, LinbikOptions options)
{
    private string? ResolveCookieDomain(HttpContext context)
        => !string.IsNullOrEmpty(options.CookieDomain) ? options.CookieDomain : context.Request.Host.Host;

    public void StoreTokens(HttpContext context, LinbikTokenResponse response)
    {
        // Store refresh token if available
        if (!string.IsNullOrEmpty(response.RefreshToken))
        {
            var refreshExpiry = (response.RefreshTokenExpiresAt ?? 0) > 0
                ? DateTimeOffset.FromUnixTimeSeconds(response.RefreshTokenExpiresAt!.Value).UtcDateTime
                : DateTime.UtcNow.AddDays(14);

            context.Response.Cookies.Append(LinbikDefaults.RefreshTokenCookie, response.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = options.SameSite,
                Path = "/",
                Expires = refreshExpiry,
                Domain = ResolveCookieDomain(context)
            });
        }

        // Store integration tokens in cookies for YARP proxy
        if (response.Integrations?.Count > 0)
        {
            StoreIntegrationCookies(context, response.Integrations);
        }

        logger.LogInformation("Tokens stored in cookies for user {UserId}", response.UserId);
    }

    public void StoreIntegrationCookies(HttpContext context, List<LinbikIntegrationToken> integrations)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = options.SameSite,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddHours(1),
            Domain = ResolveCookieDomain(context)
        };

        foreach (var integration in integrations)
        {
            var cookieName = $"{LinbikDefaults.IntegrationTokenPrefix}{integration.PackageName}";
            context.Response.Cookies.Append(cookieName, integration.Token, cookieOptions);
            logger.LogDebug("Stored integration cookie for {PackageName}", integration.PackageName);
        }
    }

    public void ClearIntegrationCookies(HttpContext context)
    {
        var deleteCookieOptions = new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = options.SameSite,
            Domain = ResolveCookieDomain(context)
        };

        // Get all cookies that start with integration_
        var integrationCookies = context.Request.Cookies.Keys
            .Where(k => k.StartsWith(LinbikDefaults.IntegrationTokenPrefix))
            .ToList();

        foreach (var cookieName in integrationCookies)
        {
            context.Response.Cookies.Delete(cookieName, deleteCookieOptions);
            logger.LogDebug("Cleared integration cookie {CookieName}", cookieName);
        }
    }
}
