using ApiGateway.Docs;
using Linbik.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiGateway.Auth;

/// <summary>
/// Gateway için authorization policy'lerini tanımlar.
/// Delegated + Application PASETO bearer şemaları <c>AddLinbikServer(...)</c>
/// tarafından kayıt edilir (Program.cs); Self (cookie) şeması ise
/// <c>AddLinbikPasetoAuth()</c> tarafından. Burada yalnız policy'ler tanımlanır.
/// </summary>
public static class LinbikGatewayAuthExtensions
{
    public const string SelfPolicy = "LinbikAuthorize";
    public const string DelegatedPolicy = "LinbikDelegatedAuthorize";
    public const string ApplicationPolicy = "LinbikApplicationAuthorize";

    /// <summary>
    /// <c>delegated.json</c>, <c>apps.json</c> JSON endpoint'leri için: cookie (Self)
    /// veya Application bearer'dan biriyle erişim. İnsan kullanıcı cookie ile,
    /// server-to-server app Application token'ı ile doc'u çekebilir.
    /// </summary>
    public const string SelfOrApplicationPolicy = "LinbikSelfOrApplicationAuthorize";

    /// <summary>
    /// <c>self.json</c>, <c>/docs/self</c> için: <see cref="LinbikGatewayOptions.Docs"/>.
    /// <see cref="LinbikDocsAuthOptions.SelfAccess"/> kuralına göre dinamik olarak karar
    /// verir (kapalı / anonim / herhangi bir kullanıcı / kullanıcı adı allow-list'i).
    /// Cookie (Self) scheme'i her zaman tetiklenir ki authenticate olmayan ziyaretçi
    /// login'e yönlendirilsin (challenge), authenticate olup kural reddedileni ise 403 alsın (forbid).
    /// </summary>
    public const string SelfDocPolicy = "LinbikSelfDocAccess";

    // Gateway YARP route'larında kullanılır. Auth scheme'i tetikler (claim üretir,
    // transform Linbik-Flow ve Linbik-{Claim} header'larını yazabilsin diye), ama
    // 401 fırlatmaz: "service-authoritative" invariantı — anonim op'lar (örn.
    // `/arithmetic/add` `linbik-flows: ["*"]`) gateway katmanında reddedilmesin.
    // Asıl güvenlik kapısı downstream servis tarafındaki `[LFlowAuthorize]`.
    public const string SelfOptional = "LinbikSelfOptional";
    public const string DelegatedOptional = "LinbikDelegatedOptional";
    public const string ApplicationOptional = "LinbikApplicationOptional";

    public static IServiceCollection AddLinbikGatewayAuth(this IServiceCollection services)
    {
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

            // Cookie (Self) VEYA Application bearer'dan birini kabul eder.
            // delegated.json + apps.json JSON endpoint'leri için: insan kullanıcı
            // cookie ile, server-to-server app Application token'ı ile erişir.
            options.AddPolicy(SelfOrApplicationPolicy, p =>
            {
                p.AddAuthenticationSchemes(LinbikDefaults.ClientScheme, LinbikDefaults.ApplicationScheme);
                p.RequireAuthenticatedUser();
            });

            // SelfAccess kuralı config'ten her istekte taze okunur (restart gerektirmez).
            // ClientScheme tetiklenir ama RequireAuthenticatedUser() çağrılmaz: "anonim"
            // modda anonim ziyaretçinin de assertion'ı geçmesi gerekir.
            options.AddPolicy(SelfDocPolicy, p =>
            {
                p.AddAuthenticationSchemes(LinbikDefaults.ClientScheme);
                p.RequireAssertion(ctx =>
                {
                    var http = (HttpContext)ctx.Resource!;
                    var docs = http.RequestServices.GetRequiredService<IOptions<LinbikGatewayOptions>>().Value.Docs;
                    return LinbikDocsAuthOptions.IsSelfAccessAllowed(docs.SelfAccess, ctx.User);
                });
            });

            // Optional policies: auth scheme'i tetikler, geçerli ise principal
            // doldurulur; geçersiz/yok ise anonim olarak devam eder. Authorization
            // her durumda OK (RequireAssertion true). Downstream `[LFlowAuthorize]`
            // kapıyı korur.
            options.AddPolicy(SelfOptional, p =>
            {
                p.AddAuthenticationSchemes(LinbikDefaults.ClientScheme);
                p.RequireAssertion(_ => true);
            });
            options.AddPolicy(DelegatedOptional, p =>
            {
                p.AddAuthenticationSchemes(LinbikDefaults.DelegatedScheme);
                p.RequireAssertion(_ => true);
            });
            options.AddPolicy(ApplicationOptional, p =>
            {
                p.AddAuthenticationSchemes(LinbikDefaults.ApplicationScheme);
                p.RequireAssertion(_ => true);
            });
        });

        return services;
    }
}
