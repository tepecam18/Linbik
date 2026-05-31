using Linbik.Core;
using Linbik.Core.Attributes;
using Linbik.Core.Responses;
using Microsoft.AspNetCore.Http;

namespace Linbik.Slices.Endpoints;

/// <summary>
/// Minimal API endpoint'lerinde <c>Linbik-Flow</c> header doğrulamasını uygulayan
/// endpoint filter. MVC tarafındaki <see cref="LFlowAuthorizeAttribute"/> action
/// filter'ı minimal API'de çalışmadığından, slice endpoint'leri bu filter'ı kullanır.
/// İkisi de ortak <see cref="LFlowGate"/> kapısını paylaşır.
/// </summary>
public sealed class LinbikFlowEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var attribute = endpoint?.Metadata.GetMetadata<LFlowAuthorizeAttribute>();
        var allowedFlows = attribute?.AllowedFlows ?? [];

        var headerValues = context.HttpContext.Request.Headers[LinbikDefaults.HeaderFlow];
        var decision = LFlowGate.Evaluate(headerValues, allowedFlows);

        if (!decision.Allowed)
        {
            return Microsoft.AspNetCore.Http.Results.Json(
                new LBaseResponse<object>(title: decision.Title, message: decision.Message, isSuccess: false),
                statusCode: decision.StatusCode);
        }

        return await next(context);
    }
}
