using Linbik.Core;
using Linbik.Core.Attributes;
using Linbik.Core.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Slices.Endpoints;

/// <summary>
/// <c>[LAuthorizeFlow]</c> ile işaretli slice'lar için gerçek authentication scheme
/// doğrulaması yapan endpoint filter. <c>Linbik-Flow</c> header'ına güvenen
/// <see cref="LinbikFlowEndpointFilter"/>'ın aksine, izin verilen her akış için
/// karşılık gelen scheme'i (bkz. <see cref="LinbikDefaults.SchemeForFlow"/>) sırayla
/// dener; ilk başarılı olan akışta durur (bir istek en fazla tek bir akış için geçerlidir).
/// Servis üzerinde kayıtlı olmayan scheme'ler (framework'ün fırlattığı
/// <see cref="InvalidOperationException"/> yerine) sessizce atlanır ve
/// yanıtta bilgilendirici bir mesajla belirtilir.
/// </summary>
public sealed class LinbikAuthorizeFlowEndpointFilter : IEndpointFilter
{
    private static readonly string[] AllFlows =
        [LinbikDefaults.Flows.Self, LinbikDefaults.Flows.Delegated, LinbikDefaults.Flows.Application];

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var endpoint = httpContext.GetEndpoint();
        var attribute = endpoint?.Metadata.GetMetadata<LFlowAuthorizeAttribute>();
        var allowedFlows = attribute?.AllowedFlows is { Length: > 0 } f ? f : AllFlows;

        var schemeProvider = httpContext.RequestServices.GetService<IAuthenticationSchemeProvider>();
        if (schemeProvider is null)
        {
            return Unauthorized("Authentication is not configured for this service.");
        }

        var registeredSchemes = (await schemeProvider.GetAllSchemesAsync())
            .Select(s => s.Name)
            .ToHashSet(StringComparer.Ordinal);

        var attemptedFlows = new List<string>();
        var unconfiguredFlows = new List<string>();

        foreach (var flow in allowedFlows)
        {
            var scheme = LinbikDefaults.SchemeForFlow(flow);
            if (scheme is null || !registeredSchemes.Contains(scheme))
            {
                unconfiguredFlows.Add(flow);
                continue;
            }

            attemptedFlows.Add(flow);
            var result = await httpContext.AuthenticateAsync(scheme);
            if (result.Succeeded && result.Principal is not null)
            {
                // Bir istek en fazla tek bir akış için geçerlidir: ilk başarılı
                // doğrulamada dur, kalan akışları denemeye gerek yok.
                httpContext.User = result.Principal;
                return await next(context);
            }
        }

        return Unauthorized(BuildMessage(attemptedFlows, unconfiguredFlows));
    }

    private static IResult Unauthorized(string message) =>
        Microsoft.AspNetCore.Http.Results.Json(
            new LBaseResponse<object>(title: "unauthorized", message: message, isSuccess: false),
            statusCode: StatusCodes.Status401Unauthorized);

    private static string BuildMessage(List<string> attemptedFlows, List<string> unconfiguredFlows)
    {
        var parts = new List<string>();

        if (attemptedFlows.Count > 0)
            parts.Add($"Identity could not be verified for flow(s): {string.Join(", ", attemptedFlows)}.");

        if (unconfiguredFlows.Count > 0)
            parts.Add($"Flow(s) {string.Join(", ", unconfiguredFlows)} are not configured on this service (missing authentication scheme registration).");

        if (parts.Count == 0)
            parts.Add("No authentication scheme is configured for the flows allowed on this endpoint.");

        return string.Join(" ", parts);
    }
}
