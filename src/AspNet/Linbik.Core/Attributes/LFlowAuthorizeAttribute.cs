using Linbik.Core.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Linbik.Core.Attributes;

/// <summary>
/// API Gateway tarafından <c>Linbik-Flow</c> header'ı ile iletilen yetkilendirme
/// akışına göre bir controller veya action'ın çağrılmasına izin verir. Header
/// listede yoksa <c>403 Forbidden</c>, hiç yoksa <c>401 Unauthorized</c> ile
/// birlikte <see cref="LBaseResponse{T}"/> formatında yanıt döner.
/// </summary>
/// <remarks>
/// Servis tarafı yetkilendirmenin son söz sahibidir. Gateway preflight yapsa bile
/// (defense in depth) bu attribute tek başına yeterli kontrolü sağlar.
/// Hiç attribute verilmezse endpoint anonim/her flow için açıktır.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class LFlowAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>
    /// Erişime izin verilen akışlar. Boş bırakılırsa yalnızca header'ın varlığı
    /// (yani gateway tarafından authenticate edilmiş herhangi bir akış) yeterlidir.
    /// </summary>
    public string[] AllowedFlows { get; }

    public LFlowAuthorizeAttribute(params string[] allowedFlows)
    {
        AllowedFlows = allowedFlows ?? Array.Empty<string>();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var headerValues = context.HttpContext.Request.Headers[LinbikDefaults.HeaderFlow];

        // Güvenlik: header birden fazla kez veya virgülle birleştirilmiş gelirse
        // bu, gateway transform'unu manipüle etme/inject etme girişimi sayılır.
        // Tek değer + tek token zorunlu.
        if (headerValues.Count > 1)
        {
            context.Result = new ObjectResult(new LBaseResponse<object>(
                title: "unauthorized",
                message: $"Header '{LinbikDefaults.HeaderFlow}' must appear exactly once.",
                isSuccess: false))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        var flow = headerValues.ToString().Trim();

        if (string.IsNullOrEmpty(flow))
        {
            context.Result = new ObjectResult(new LBaseResponse<object>(
                title: "unauthorized",
                message: $"Required header '{LinbikDefaults.HeaderFlow}' is missing. Requests must be routed through the API Gateway.",
                isSuccess: false))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        // Virgül, boşluk veya kontrol karakteri içeren değerleri reddet —
        // tek bir flow tokenı bekliyoruz.
        if (flow.IndexOfAny(new[] { ',', ';', ' ', '\t', '\r', '\n' }) >= 0)
        {
            context.Result = new ObjectResult(new LBaseResponse<object>(
                title: "unauthorized",
                message: $"Header '{LinbikDefaults.HeaderFlow}' contains invalid characters.",
                isSuccess: false))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        if (AllowedFlows.Length > 0 &&
            !AllowedFlows.Contains(flow, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new LBaseResponse<object>(
                title: "forbidden_flow",
                message: $"This operation is not allowed for the '{flow}' flow. Allowed: {string.Join(", ", AllowedFlows)}.",
                isSuccess: false))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
