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

        var decision = LFlowGate.Evaluate(headerValues, AllowedFlows);
        if (!decision.Allowed)
        {
            context.Result = new ObjectResult(new LBaseResponse<object>(
                title: decision.Title,
                message: decision.Message,
                isSuccess: false))
            {
                StatusCode = decision.StatusCode
            };
            return;
        }

        await next();
    }
}
