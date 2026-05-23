using Linbik.Core.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Linbik.Gateway.Sample.Gateway.Authentication;

/// <summary>
/// PASETO v4.public (Ed25519) token doğrulaması yapan ASP.NET Core authentication handler.
/// <para>
/// <b>Linbik.PasetoAuthManager</b> paketi hazır olduğunda bu handler yerine
/// <c>builder.AddLinbikPasetoAuth()</c> çağrılabilir; gateway kodu değişmez.
/// </para>
/// </summary>
public sealed class PasetoAuthenticationHandler(
    IOptionsMonitor<PasetoAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IPasetoHelper pasetoHelper)
    : AuthenticationHandler<PasetoAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtractToken();

        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        // KeylessMode: geliştirme ortamı için imza doğrulamasını atla
        if (Options.KeylessMode)
        {
            var claims = pasetoHelper.GetTokenClaims(token);
            if (claims.Count == 0)
                return AuthenticateResult.Fail("KeylessMode: token parse edilemedi.");

            return Success(claims);
        }

        if (string.IsNullOrEmpty(Options.PublicKeyBase64))
            return AuthenticateResult.Fail("PASETO public key yapılandırılmamış.");

        // Token doğrulama
        var isValid = await pasetoHelper.ValidateTokenAsync(
            token,
            Options.PublicKeyBase64,
            Options.ExpectedAudience,
            Options.ExpectedIssuer);

        if (!isValid)
            return AuthenticateResult.Fail("PASETO token geçersiz veya süresi dolmuş.");

        // Claim extraction (doğrulama sonrası)
        var tokenClaims = pasetoHelper.GetTokenClaims(token);
        return Success(tokenClaims);
    }

    // ── Challenge & Forbid ───────────────────────────────────────────────

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private string? ExtractToken() => Options.TokenSource switch
    {
        PasetoTokenSource.Cookie => Request.Cookies[Options.CookieName],
        PasetoTokenSource.Bearer => ExtractBearerToken(),
        _ => null
    };

    private string? ExtractBearerToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return authHeader["Bearer ".Length..].Trim();
    }

    private AuthenticateResult Success(Dictionary<string, string> tokenClaims)
    {
        var claims = tokenClaims
            .Select(kv => new Claim(kv.Key, kv.Value))
            .ToList();

        // Scheme adını AuthenticationType olarak gömüyoruz; ClaimToHeaderTransform bunu okur.
        var identity  = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
