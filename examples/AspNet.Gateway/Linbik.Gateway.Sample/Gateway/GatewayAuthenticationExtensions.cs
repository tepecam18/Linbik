using Linbik.Core;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.Gateway.Sample.Gateway.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Gateway.Sample.Gateway;

/// <summary>
/// Gateway'e özgü 3 PASETO scheme + 3 authorization policy kaydı.
/// <para>
/// <b>Swap notu:</b> <c>Linbik.PasetoAuthManager</c> paketi yayınlandığında
/// bu dosyadaki <c>AddPasetoScheme()</c> çağrıları kaldırılıp
/// <c>builder.AddLinbikPasetoAuth()</c> tek satırla değiştirilebilir.
/// </para>
/// </summary>
public static class GatewayAuthenticationExtensions
{
    // appsettings.json yolları
    private const string SelfKey      = "LinbikGateway:Auth:Self";
    private const string DelegatedKey = "LinbikGateway:Auth:Delegated";
    private const string AppsKey      = "LinbikGateway:Auth:Apps";

    /// <summary>
    /// PASETO v4.public authentication için 3 scheme + 3 policy ekler.
    /// Her scheme <see cref="PasetoAuthenticationHandler"/> kullanır.
    /// </summary>
    public static IServiceCollection AddLinbikGatewayAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // IPasetoHelper → PasetoHelperService (Linbik.Core)
        services.AddSingleton<IPasetoHelper, PasetoHelperService>();

        var selfSection      = configuration.GetSection(SelfKey);
        var delegatedSection = configuration.GetSection(DelegatedKey);
        var appsSection      = configuration.GetSection(AppsKey);

        services
            .AddAuthentication()
            // ── Self: authToken cookie, PASETO v4.public ────────────────
            .AddPasetoScheme(
                LinbikDefaults.ClientScheme,
                opts =>
                {
                    opts.TokenSource      = PasetoTokenSource.Cookie;
                    opts.CookieName       = LinbikDefaults.AuthTokenCookie;
                    opts.PublicKeyBase64  = selfSection["PublicKey"] ?? string.Empty;
                    opts.ExpectedAudience = selfSection["Audience"]  ?? string.Empty;
                    opts.KeylessMode      = selfSection.GetValue<bool>("KeylessMode");
                })
            // ── Delegated: Bearer, kullanıcı bağlamlı token ─────────────
            .AddPasetoScheme(
                LinbikDefaults.DelegatedScheme,
                opts =>
                {
                    opts.TokenSource      = PasetoTokenSource.Bearer;
                    opts.PublicKeyBase64  = delegatedSection["PublicKey"] ?? string.Empty;
                    opts.ExpectedAudience = delegatedSection["Audience"]  ?? string.Empty;
                    opts.KeylessMode      = delegatedSection.GetValue<bool>("KeylessMode");
                })
            // ── Apps: Bearer, S2S token ──────────────────────────────────
            .AddPasetoScheme(
                LinbikDefaults.ApplicationScheme,
                opts =>
                {
                    opts.TokenSource      = PasetoTokenSource.Bearer;
                    opts.PublicKeyBase64  = appsSection["PublicKey"] ?? string.Empty;
                    opts.ExpectedAudience = appsSection["Audience"]  ?? string.Empty;
                    opts.KeylessMode      = appsSection.GetValue<bool>("KeylessMode");
                });

        // ── Authorization policies ────────────────────────────────────────
        services.AddAuthorization(authz =>
        {
            authz.AddPolicy(LinbikGatewayDefaults.PolicySelf, p => p
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(LinbikDefaults.ClientScheme));

            authz.AddPolicy(LinbikGatewayDefaults.PolicyDelegated, p => p
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(LinbikDefaults.DelegatedScheme));

            authz.AddPolicy(LinbikGatewayDefaults.PolicyApps, p => p
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(LinbikDefaults.ApplicationScheme));
        });

        return services;
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private static AuthenticationBuilder AddPasetoScheme(
        this AuthenticationBuilder builder,
        string schemeName,
        Action<PasetoAuthenticationOptions> configure)
    {
        return builder.AddScheme<PasetoAuthenticationOptions, PasetoAuthenticationHandler>(
            schemeName, configure);
    }
}
