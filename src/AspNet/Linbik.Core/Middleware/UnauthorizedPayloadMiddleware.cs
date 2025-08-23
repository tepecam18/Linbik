using Linbik.Core.Services;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Linbik.Core.Middleware;

public sealed class UnauthorizedPayloadMiddleware
{
    private const string JsonContentType = "application/json; charset=utf-8";
    
    private readonly RequestDelegate _next;

    public UnauthorizedPayloadMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        var statusCode = context.Response.StatusCode;
        if ((statusCode == StatusCodes.Status401Unauthorized || statusCode == StatusCodes.Status403Forbidden)
            && !context.Response.HasStarted)
        {
            context.Response.ContentType = JsonContentType;

            var payload = PkceService.BuildAuthorizeBody(context.Response);

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
