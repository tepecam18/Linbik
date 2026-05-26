using Microsoft.AspNetCore.Http;

namespace Linbik.Core.Models;

/// <summary>
/// API Gateway tarafından doğrulanan PASETO token'ından parse edilip
/// downstream servislere <c>Linbik-*</c> header'ları olarak iletilen
/// kimlik bağlamı. Servisler bu yapıyı response payload'larına ekleyerek
/// gateway'in tanıdığı çağrı sahibini istemciye bildirebilir.
/// </summary>
public sealed class LGatewayAuthContext
{
    /// <summary>
    /// Gateway tarafından eşleştirilen yetkilendirme akışı:
    /// <c>"Self"</c>, <c>"Delegated"</c> veya <c>"Application"</c>.
    /// Header karşılığı: <c>Linbik-Flow</c>.
    /// </summary>
    public string? Flow { get; set; }

    /// <summary>
    /// Gateway'in doğrulanmış token'dan çıkardığı tüm claim'ler.
    /// Anahtar claim type'ın header-friendly hali, değer bir veya birden
    /// fazla claim değerinin koleksiyonudur.
    /// </summary>
    public Dictionary<string, string[]> Claims { get; set; } = new();

    /// <summary>
    /// Gelen HTTP isteğinde gateway tarafından eklenen <c>Linbik-*</c>
    /// header'larından yetkilendirme bağlamını üretir.
    /// </summary>
    public static LGatewayAuthContext FromRequest(HttpRequest request)
    {
        const string prefix = "Linbik-";
        const string flowHeader = "Linbik-Flow";

        var ctx = new LGatewayAuthContext();

        if (request.Headers.TryGetValue(flowHeader, out var flow))
        {
            ctx.Flow = flow.ToString();
        }

        foreach (var header in request.Headers)
        {
            if (!header.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            // Flow zaten ayrı tutuldu; Claims sözlüğüne tekrar eklemeyelim.
            if (string.Equals(header.Key, flowHeader, StringComparison.OrdinalIgnoreCase))
                continue;

            var claimName = header.Key[prefix.Length..];
            ctx.Claims[claimName] = header.Value.ToArray()!;
        }

        return ctx;
    }
}
