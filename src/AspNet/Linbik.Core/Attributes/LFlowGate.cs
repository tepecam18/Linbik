using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Linbik.Core.Attributes;

/// <summary>
/// <c>Linbik-Flow</c> header doğrulamasının tek doğruluk kaynağı. Hem MVC
/// <see cref="LFlowAuthorizeAttribute"/> action filter'ı hem de minimal API
/// endpoint filter'ı bu kapıyı paylaşır; böylece güvenlik mantığı tek yerde durur.
/// </summary>
public static class LFlowGate
{
    /// <summary>Doğrulama sonucu. <see cref="Allowed"/> false ise HTTP yanıtı üretilmelidir.</summary>
    public readonly record struct Decision(bool Allowed, int StatusCode, string Title, string Message)
    {
        /// <summary>İzin verilen istek için sonuç.</summary>
        public static Decision Allow { get; } = new(true, 0, string.Empty, string.Empty);
    }

    /// <summary>
    /// <paramref name="headerValues"/> içindeki <c>Linbik-Flow</c> değerini
    /// <paramref name="allowedFlows"/> kümesine göre doğrular.
    /// </summary>
    /// <param name="headerValues">İstekteki <c>Linbik-Flow</c> header değer(ler)i.</param>
    /// <param name="allowedFlows">
    /// İzin verilen akışlar. Boş ise yalnızca geçerli (tek, temiz) bir header varlığı yeterlidir.
    /// <c>"*"</c> içeriyorsa endpoint public kabul edilir ve header gerekmez.
    /// </param>
    public static Decision Evaluate(StringValues headerValues, string[] allowedFlows)
    {
        allowedFlows ??= [];

        // Public (anonim) endpoint: header şartı yok.
        if (Array.IndexOf(allowedFlows, "*") >= 0)
            return Decision.Allow;

        // Güvenlik: header birden fazla kez gelirse gateway transform'unu
        // manipüle etme girişimi sayılır. Tek değer zorunlu.
        if (headerValues.Count > 1)
        {
            return new Decision(false, StatusCodes.Status401Unauthorized, "unauthorized",
                $"Header '{LinbikDefaults.HeaderFlow}' must appear exactly once.");
        }

        var flow = headerValues.ToString().Trim();

        if (string.IsNullOrEmpty(flow))
        {
            return new Decision(false, StatusCodes.Status401Unauthorized, "unauthorized",
                $"Required header '{LinbikDefaults.HeaderFlow}' is missing. Requests must be routed through the API Gateway.");
        }

        // Virgül, boşluk veya kontrol karakteri içeren değerleri reddet —
        // tek bir flow tokenı bekliyoruz.
        if (flow.IndexOfAny([',', ';', ' ', '\t', '\r', '\n']) >= 0)
        {
            return new Decision(false, StatusCodes.Status401Unauthorized, "unauthorized",
                $"Header '{LinbikDefaults.HeaderFlow}' contains invalid characters.");
        }

        if (allowedFlows.Length > 0 &&
            !allowedFlows.Contains(flow, StringComparer.OrdinalIgnoreCase))
        {
            return new Decision(false, StatusCodes.Status403Forbidden, "forbidden_flow",
                $"This operation is not allowed for the '{flow}' flow. Allowed: {string.Join(", ", allowedFlows)}.");
        }

        return Decision.Allow;
    }
}
