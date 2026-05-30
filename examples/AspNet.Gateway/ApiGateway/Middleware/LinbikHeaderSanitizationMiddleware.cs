using Microsoft.AspNetCore.Http;

namespace ApiGateway.Middleware;

/// <summary>
/// Gateway pipeline'ının en üstünde çalışır. Gelen istekteki tüm
/// <c>Linbik-*</c> header'larını siler; böylece dış istemciler claim
/// header'larını spoof edemez. Header'lar yalnızca PASETO doğrulamasından
/// sonra <c>LinbikClaimsHeaderTransform</c> tarafından yeniden eklenir.
/// </summary>
public sealed class LinbikHeaderSanitizationMiddleware
{
    public const string HeaderPrefix = "Linbik-";

    private readonly RequestDelegate _next;

    public LinbikHeaderSanitizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var toRemove = context.Request.Headers.Keys
            .Where(k => k.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var key in toRemove)
        {
            context.Request.Headers.Remove(key);
        }

        return _next(context);
    }
}
