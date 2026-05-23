using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Linbik.Gateway.Sample.Gateway;

/// <summary>
/// YARP request transform: inbound <c>X-Linbik-*</c> header'larını temizler,
/// ardından <see cref="HttpContext.User"/> claim'lerini allowlist'e göre
/// downstream header'larına yazar.
///
/// <para>Güvenlik notu: Sanitize adımı claim injection'dan ÖNCE çalışır;
/// dışarıdan gelen spoof girişimleri etkisiz kalır.</para>
/// </summary>
public sealed class ClaimToHeaderTransform
{
    private readonly IReadOnlyDictionary<string, string> _claimHeaderMap;
    private readonly string _gatewayName;

    public ClaimToHeaderTransform(
        IReadOnlyDictionary<string, string>? claimHeaderMap = null,
        string gatewayName = "Linbik.Gateway.Sample")
    {
        _claimHeaderMap = claimHeaderMap ?? LinbikGatewayDefaults.DefaultClaimHeaderMap;
        _gatewayName = gatewayName;
    }

    /// <inheritdoc/>
    public ValueTask ApplyAsync(RequestTransformContext context)
    {
        var headers = context.ProxyRequest.Headers;

        // ── 1. Sanitize ──────────────────────────────────────────────────
        // X-Linbik-* ile başlayan tüm header'ları sil (spoof önleme).
        var toRemove = headers
            .Select(h => h.Key)
            .Where(k => k.StartsWith(LinbikGatewayDefaults.HeaderPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in toRemove)
            headers.Remove(key);

        // Authorization header'ı downstream'e iletme (JWT/PASETO görmemeli).
        headers.Remove("Authorization");

        // ── 2. Claim injection ───────────────────────────────────────────
        var user = context.HttpContext.User;
        if (!user.Identity?.IsAuthenticated ?? true)
            return ValueTask.CompletedTask;

        // Allowlist: role claim'i tek değerde birleştir
        var roleValues = user.Claims
            .Where(c => string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToList();

        // Yazılmış role header'ını takip et (duplicate'i önle)
        bool roleWritten = false;

        foreach (var claim in user.Claims)
        {
            if (!_claimHeaderMap.TryGetValue(claim.Type, out var headerName))
                continue;

            // Roles birleştirme: ilkinde tüm değerleri yaz, sonrakileri atla
            if (headerName == "X-Linbik-Roles")
            {
                if (roleWritten) continue;
                headers.TryAddWithoutValidation(headerName, string.Join(",", roleValues));
                roleWritten = true;
                continue;
            }

            // Diğer claim'ler: çoğaltma kontrolü
            if (!headers.Contains(headerName))
                headers.TryAddWithoutValidation(headerName, claim.Value);
        }

        // ── 3. Meta header'lar ───────────────────────────────────────────
        // Hangi scheme ile doğrulandığını downstream'e bildir.
        var schemeLabel = ResolveSchemeLabel(user);
        if (!string.IsNullOrEmpty(schemeLabel))
            headers.TryAddWithoutValidation(LinbikGatewayDefaults.HeaderScheme, schemeLabel);

        headers.TryAddWithoutValidation(LinbikGatewayDefaults.HeaderGateway, _gatewayName);

        return ValueTask.CompletedTask;
    }

    private static string ResolveSchemeLabel(ClaimsPrincipal user)
    {
        // ASP.NET Core Identity.AuthenticationType = scheme name
        var authType = user.Identity?.AuthenticationType;
        return authType switch
        {
            Core.LinbikDefaults.ClientScheme      => LinbikGatewayDefaults.SchemeLabelSelf,
            Core.LinbikDefaults.DelegatedScheme   => LinbikGatewayDefaults.SchemeLabelDelegated,
            Core.LinbikDefaults.ApplicationScheme => LinbikGatewayDefaults.SchemeLabelApps,
            _ => string.Empty
        };
    }
}

/// <summary>
/// <see cref="ClaimToHeaderTransform"/>'ı YARP transform pipeline'ına ekleyen uzantı.
/// </summary>
public static class ClaimToHeaderTransformExtensions
{
    public static IReverseProxyBuilder AddLinbikClaimTransform(
        this IReverseProxyBuilder builder,
        IReadOnlyDictionary<string, string>? claimHeaderMap = null,
        string gatewayName = "Linbik.Gateway.Sample")
    {
        var transform = new ClaimToHeaderTransform(claimHeaderMap, gatewayName);
        builder.AddTransforms(b => b.AddRequestTransform(transform.ApplyAsync));
        return builder;
    }
}
