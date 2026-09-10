using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Linbik.Core.Services;

/// <summary>Shared endpoint behavior for JWT and PASETO authentication.</summary>
internal static class LinbikAuthEndpointHelpers
{
    private const string AuthTokenCookie = LinbikDefaults.AuthTokenCookie;
    private const string LinbikRefreshTokenCookie = LinbikDefaults.RefreshTokenCookie;
    private const string UserNameCookie = LinbikDefaults.UserNameCookie;
    private const string IntegrationTokenPrefix = LinbikDefaults.IntegrationTokenPrefix;

    internal static DateTime CalculateExpiry(long? unixTimestamp, DateTime defaultExpiry)
    {
        return unixTimestamp.HasValue && unixTimestamp.Value > 0
            ? DateTimeOffset.FromUnixTimeSeconds(unixTimestamp.Value).UtcDateTime
            : defaultExpiry;
    }

    internal static void SetAuthCookies(
        HttpContext context,
        LinbikTokenResponse tokenResponse,
        string accessToken,
        DateTime accessTokenExpiry,
        DateTime refreshTokenExpiry,
        string cookieDomain,
        SameSiteMode sameSite)
    {
        if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
        {
            context.Response.Cookies.Append(LinbikRefreshTokenCookie, tokenResponse.RefreshToken, CreateCookieOptions(refreshTokenExpiry, cookieDomain, sameSite, httpOnly: true));
        }

        if (tokenResponse.Integrations?.Count > 0)
        {
            foreach (var integration in tokenResponse.Integrations)
            {
                var cookieName = $"{IntegrationTokenPrefix}{integration.PackageName}";
                context.Response.Cookies.Append(cookieName, integration.Token, CreateCookieOptions(accessTokenExpiry, cookieDomain, sameSite, httpOnly: true));
            }
        }

        context.Response.Cookies.Append(AuthTokenCookie, accessToken, CreateCookieOptions(accessTokenExpiry, cookieDomain, sameSite, httpOnly: true));

        context.Response.Cookies.Append(UserNameCookie, tokenResponse.Username, CreateCookieOptions(refreshTokenExpiry, cookieDomain, sameSite, httpOnly: false));
    }

    private static CookieOptions CreateCookieOptions(
        DateTime expiry, string domain, SameSiteMode sameSite, bool httpOnly) => new()
        {
            HttpOnly = httpOnly,
            Secure = true,
            SameSite = sameSite,
            Expires = expiry,
            Path = "/",
            Domain = domain
        };

    internal static bool IsMobileClient(LinbikClientConfig? clientConfig)
        => clientConfig?.ActionResultType == ActionResultType.Json;

    internal static LinbikClientConfig? GetClientConfig(LinbikOptions linbikOptions, string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        return linbikOptions.Clients.FirstOrDefault(c => c.Name == name);
    }

    internal static string AppendMessageToUrl(string baseUrl, string message, bool isError = false)
    {
        var paramName = isError ? "error" : "message";
        return QueryHelpers.AddQueryString(baseUrl, paramName, message);
    }

    internal static IResult ReturnAuthError(
        LinbikClientConfig? clientConfig,
        string? redirectPath,
        string errorMessage,
        int statusCode = 400)
    {
        if (!IsMobileClient(clientConfig) && !string.IsNullOrEmpty(redirectPath))
        {
            var redirectUrl = AppendMessageToUrl(redirectPath, errorMessage, isError: true);
            return Results.Redirect(redirectUrl);
        }

        return statusCode switch
        {
            401 => Results.Unauthorized(),
            403 => Results.Forbid(),
            _ => Results.BadRequest(new LBaseResponse<object>(errorMessage))
        };
    }
}
