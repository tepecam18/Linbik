using ApiGateway.Middleware;
using Linbik.Core;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;

namespace ApiGateway.Transforms;

/// <summary>
/// Doğrulanmış <see cref="System.Security.Claims.ClaimsPrincipal"/> içindeki
/// claim'leri downstream isteğine <c>Linbik-{ClaimType}</c> header'ları olarak
/// enjekte eder. Ayrıca eşleşen YARP route'unun <c>Linbik-Flow</c> metadata'sını
/// <c>Linbik-Flow</c> header'ı olarak downstream'e iletir. Gateway tarafından
/// kullanılan ham token başlık/cookie'leri downstream'e iletilmez.
/// </summary>
public sealed class LinbikClaimsHeaderTransform : RequestTransform
{
    public const string FlowMetadataKey = LinbikDefaults.HeaderFlow;

    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        // Downstream'e ham token taşımak istemiyoruz.
        context.ProxyRequest.Headers.Remove("Authorization");
        context.ProxyRequest.Headers.Remove("Cookie");

        var principal = context.HttpContext.User;
        var isAuthenticated = principal?.Identity?.IsAuthenticated == true;

        // ÖNEMLİ: Linbik-Flow header'ı **yalnız authenticated** istekler için yazılır.
        // Anonim istemciye flow header inject edilirse service-side `[LFlowAuthorize]`
        // (kimlik kontrolü yapmaz, yalnız header varlığını/değerini doğrular) yanlışlıkla
        // izin verir. Anonim istek → flow header yok → attribute 401 → service kapısı sağlam.
        // Anonim op'lar (`linbik-flows: ["*"]`) attribute taşımadığı için zaten geçer.
        if (isAuthenticated)
        {
            var routeMetadata = context.HttpContext.GetReverseProxyFeature()?.Route?.Config?.Metadata;
            if (routeMetadata is not null &&
                routeMetadata.TryGetValue(FlowMetadataKey, out var flow) &&
                !string.IsNullOrWhiteSpace(flow))
            {
                context.ProxyRequest.Headers.Remove(LinbikDefaults.HeaderFlow);
                context.ProxyRequest.Headers.TryAddWithoutValidation(LinbikDefaults.HeaderFlow, flow);
            }
        }

        if (!isAuthenticated || principal is null)
            return ValueTask.CompletedTask;

        // Claim grupları (aynı claim type birden fazla değer alabilir).
        foreach (var group in principal.Claims.GroupBy(c => c.Type))
        {
            var headerName = LinbikHeaderSanitizationMiddleware.HeaderPrefix + SanitizeHeaderName(group.Key);
            var values = group.Select(c => c.Value).ToArray();
            context.ProxyRequest.Headers.Remove(headerName);
            context.ProxyRequest.Headers.TryAddWithoutValidation(headerName, values);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Claim type bazen URI biçiminde gelir (ör. ClaimTypes.NameIdentifier).
    /// Header adında kullanılamayacak karakterleri '-' ile değiştirir.
    /// </summary>
    private static string SanitizeHeaderName(string claimType)
    {
        // URI tipi claim'lerden son segmenti al.
        var lastSlash = claimType.LastIndexOf('/');
        var name = lastSlash >= 0 ? claimType[(lastSlash + 1)..] : claimType;

        Span<char> buffer = stackalloc char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            buffer[i] = char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '-';
        }
        return new string(buffer);
    }
}
