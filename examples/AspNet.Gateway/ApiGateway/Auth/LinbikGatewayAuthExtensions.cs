using Linbik.Core;
using Linbik.Core.Extensions;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.PasetoAuthManager.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiGateway.Auth;

/// <summary>
/// Gateway için Delegated ve Application PASETO bearer şemalarını kayıt eder
/// ve <c>LinbikAuthorize</c> / <c>LinbikDelegatedAuthorize</c> /
/// <c>LinbikApplicationAuthorize</c> policy'lerini tanımlar.
/// </summary>
public static class LinbikGatewayAuthExtensions
{
    public const string SelfPolicy = "LinbikAuthorize";
    public const string DelegatedPolicy = "LinbikDelegatedAuthorize";
    public const string ApplicationPolicy = "LinbikApplicationAuthorize";

    public static IServiceCollection AddLinbikGatewayAuth(this IServiceCollection services)
    {
        // Delegated + Application bearer şemalarını ekle.
        services.AddAuthentication()
            .AddLinbikPasetoBearer(LinbikDefaults.DelegatedScheme, _ => { })
            .AddLinbikPasetoBearer(LinbikDefaults.ApplicationScheme, _ => { });

        // Delegated şema options'ı: Authorization: Bearer ile gelen kullanıcı token'ı.
        services.AddOptions<PasetoBearerOptions>(LinbikDefaults.DelegatedScheme)
            .Configure<IOptions<PasetoAuthOptions>>((bearer, paseto) =>
            {
                ApplyPasetoSettings(bearer, paseto.Value, requireApplicationToken: false);
            });

        // Application şema options'ı: Authorization: Bearer ile gelen S2S token'ı.
        services.AddOptions<PasetoBearerOptions>(LinbikDefaults.ApplicationScheme)
            .Configure<IOptions<PasetoAuthOptions>>((bearer, paseto) =>
            {
                ApplyPasetoSettings(bearer, paseto.Value, requireApplicationToken: true);
            });

        // Üç policy: PasetoAuthManager.AddLinbikPasetoAuth() zaten "LinbikAuthorize"
        // policy'sini kayıt ediyor; biz Delegated ve Application'ı ekliyoruz.
        services.AddAuthorization(options =>
        {
            options.AddPolicy(DelegatedPolicy, p =>
            {
                p.AddAuthenticationSchemes(LinbikDefaults.DelegatedScheme);
                p.RequireAuthenticatedUser();
            });

            options.AddPolicy(ApplicationPolicy, p =>
            {
                p.AddAuthenticationSchemes(LinbikDefaults.ApplicationScheme);
                p.RequireAuthenticatedUser();
            });
        });

        return services;
    }

    private static void ApplyPasetoSettings(
        PasetoBearerOptions bearer,
        PasetoAuthOptions paseto,
        bool requireApplicationToken)
    {
        bearer.Mode = paseto.Mode;
        bearer.ExpectedAudience = paseto.Audience;
        bearer.ExpectedIssuer = paseto.Issuer;
        bearer.RequireApplicationToken = requireApplicationToken;

        if (paseto.Mode == PasetoMode.Local)
        {
            bearer.SharedKey = paseto.SharedKeyBase64 ?? string.Empty;
        }
        else
        {
            bearer.PublicKey = paseto.PublicKeyBase64 ?? string.Empty;
        }
        // TokenRetriever atanmadı → varsayılan davranış (Authorization: Bearer header).
    }
}
