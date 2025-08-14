using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Linbik.Core;

public sealed class UnauthorizedPayloadMiddleware
{
    private readonly RequestDelegate _next;

    public UnauthorizedPayloadMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx)
    {
        await _next(ctx);

        var sc = ctx.Response.StatusCode;
        if ((sc == StatusCodes.Status401Unauthorized || sc == StatusCodes.Status403Forbidden)
            && !ctx.Response.HasStarted)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";

            object payload;

            payload = PkceService.BuildAuthorizeBody(ctx.Response);


            await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
