using ApiGateway.Middleware;
using Linbik.Core.Identity;
using Linbik.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Linbik.Tests;

public sealed class GatewayIdentityTests
{
    [Theory]
    [InlineData("Self", "Linbik-SUB")]
    [InlineData("self", "linbik-sub")]
    [InlineData(" SELF ", "LINBIK-Sub")]
    public void HttpHeaderCasingDoesNotChangeActorIdentity(string flow, string userHeader)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Linbik-Flow"] = flow;
        context.Request.Headers[userHeader] = "user-id";
        var actor = LActor.FromGatewayContext(LGatewayAuthContext.FromRequest(context.Request));
        Assert.Equal("user-id", Assert.IsType<LActor.AsUser>(actor).UserId);
    }

    [Fact]
    public void ApplicationActorNeverUsesSubjectAsUserId()
    {
        var actor = LActor.FromGatewayContext(new LGatewayAuthContext
        { Flow = "application", Claims = new() { ["SUB"] = ["app-id"] } });
        Assert.Equal("app-id", actor.AppIdOrNull());
        Assert.Null(actor.UserIdOrNull());
    }

    [Fact]
    public async Task GatewayRemovesUntrustedIdentityHeadersBeforeNextMiddleware()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["LINBIK-SUB"] = "spoofed";
        context.Request.Headers["Linbik-Flow"] = "Self";
        context.Request.Headers.Authorization = "Bearer test-token";
        var called = false;
        var middleware = new LinbikHeaderSanitizationMiddleware(next =>
        {
            called = true;
            Assert.False(next.Request.Headers.ContainsKey("LINBIK-SUB"));
            Assert.False(next.Request.Headers.ContainsKey("Linbik-Flow"));
            Assert.Equal("Bearer test-token", next.Request.Headers.Authorization.ToString());
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context);
        Assert.True(called);
    }
}
