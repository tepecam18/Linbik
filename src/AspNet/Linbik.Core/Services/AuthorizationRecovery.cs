using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Linbik.Core.Configuration;
using Linbik.Core.Responses;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Core.Services;

/// <summary>Correlates failure callbacks after the short-lived PKCE cookie has expired.</summary>
internal static class AuthorizationRecovery
{
    private const string CookieName = "linbik_auth_recovery";
    private sealed record Recovery(string State, string ClientName);
    private static CookieOptions CookieOptions => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/"
    };

    private static ITimeLimitedDataProtector Protector(HttpContext context) => context.RequestServices
        .GetRequiredService<IDataProtectionProvider>().CreateProtector("Linbik.AuthorizationRecovery.v1")
        .ToTimeLimitedDataProtector();

    internal static string Save(HttpContext context, string clientName)
    {
        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var cookie = CookieOptions;
        cookie.Expires = DateTimeOffset.UtcNow.AddMinutes(30);
        context.Response.Cookies.Append(CookieName,
            Protector(context).Protect(JsonSerializer.Serialize(new Recovery(state, clientName)), TimeSpan.FromMinutes(30)), cookie);
        return state;
    }

    internal static void Clear(HttpContext context) => context.Response.Cookies.Delete(CookieName, CookieOptions);

    internal static IResult? HandleError(HttpContext context, LinbikOptions options)
    {
        if (!context.Request.Query.ContainsKey("error")) return null;
        // Do not consume cookies for unrelated, stale, or forged callbacks.
        try
        {
            var cookie = context.Request.Cookies[CookieName];
            var state = context.Request.Query["state"].ToString();
            if (string.IsNullOrEmpty(cookie) || state.Length != 43)
                return Results.BadRequest(new LBaseResponse<object>("Login request expired or invalid. Start sign-in again."));
            var recovery = JsonSerializer.Deserialize<Recovery>(Protector(context).Unprotect(cookie));
            if (recovery is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(recovery.State), Encoding.UTF8.GetBytes(state)))
                return Results.BadRequest(new LBaseResponse<object>("Invalid login state."));
            var client = LinbikAuthEndpointHelpers.GetClientConfig(options, recovery.ClientName);
            if (client is null) return Results.BadRequest(new LBaseResponse<object>("Login client not found."));
            Clear(context);
            PkceService.DeleteVerifier(context.Response);
            var expired = context.Request.Query["error"] == "authorization_expired";
            var message = expired ? "Giriş işleminin süresi doldu. Lütfen tekrar giriş yapın." : "Giriş işlemi tamamlanamadı. Lütfen tekrar deneyin.";
            if (LinbikAuthEndpointHelpers.IsMobileClient(client))
                return Results.BadRequest(new LBaseResponse<object>(expired ? "authorization_expired" : "authorization_failed", message));
            var destination = string.IsNullOrEmpty(client.RedirectUrl) ? "/" : client.RedirectUrl;
            return Results.Redirect(QueryHelpers.AddQueryString(destination, new Dictionary<string, string?>
            {
                ["error"] = message,
                ["error_code"] = expired ? "authorization_expired" : "authorization_failed"
            }));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return Results.BadRequest(new LBaseResponse<object>("Login request expired or invalid. Start sign-in again."));
        }
    }
}
