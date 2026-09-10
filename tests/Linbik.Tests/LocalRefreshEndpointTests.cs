using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Linbik.Core;
using Linbik.Core.Extensions;
using Linbik.Core.Models;
using Linbik.Core.Services.Interfaces;
using Linbik.Core.Services;
using Linbik.Core.Configuration;
using Linbik.JwtAuthManager.Extensions;
using Linbik.PasetoAuthManager.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace Linbik.Tests;

public class LocalRefreshEndpointTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UpstreamRefreshPreservesIdentityAndUpdatesCookiesOrRejectsWithoutWriting(bool paseto, bool invalid)
    {
        var userId = Guid.NewGuid();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var options = new LinbikOptions { ServiceId = Guid.NewGuid().ToString() };
        var linbik = builder.Services.AddLinbik(o => o.EnableHeartbeat = false);
        if (paseto) linbik.AddLinbikPasetoAuth(o => o.PkceEnabled = false);
        else linbik.AddLinbikJwtAuth(o => o.PkceEnabled = false);
        builder.Services.AddLinbikRateLimiting();
        using var upstreamHttp = new HttpClient(new RefreshHandler(userId, invalid));
        builder.Services.AddSingleton<ILinbikAuthClient>(new LinbikAuthClient(upstreamHttp, Options.Create(options), NullLogger<LinbikAuthClient>.Instance));
        await using var app = builder.Build();
        app.UseRouting();
        app.UseLinbikRateLimiting();
        app.UseAuthentication();
        app.UseAuthorization();
        if (paseto) app.UseLinbikPasetoAuth();
        else app.UseLinbikJwtAuth();
        app.MapGet("/identity", (HttpContext context) => Results.Json(new
        {
            userId = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            username = context.User.FindFirst("preferred_username")?.Value
        })).RequireAuthorization("LinbikAuthorize");
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Linbik/refresh");
        request.Headers.Add("Cookie", LinbikDefaults.RefreshTokenCookie + "=upstream-old");
        using var response = await client.SendAsync(request);
        if (invalid)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            return;
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(LinbikDefaults.RefreshTokenCookie + "=upstream-new", RefreshCookie(response));
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, c => c.StartsWith("integration_test-service=integration-new;"));
        using var identityRequest = new HttpRequestMessage(HttpMethod.Get, "/identity");
        identityRequest.Headers.Add("Cookie", cookies.Single(c => c.StartsWith(LinbikDefaults.AuthTokenCookie + "=")).Split(';')[0]);
        using var identity = await client.SendAsync(identityRequest);
        Assert.Equal(HttpStatusCode.OK, identity.StatusCode);
        var profile = await identity.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        Assert.Equal(userId.ToString(), profile!["userId"]!.GetValue<string>());
        Assert.Equal("test-user", profile["username"]!.GetValue<string>());
    }

    private sealed class RefreshHandler(Guid userId, bool invalid) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("upstream-old", Assert.Single(request.Headers.GetValues("RefreshToken")));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    isSuccess = true,
                    data = new LinbikTokenResponse
                    {
                        UserId = invalid ? Guid.Empty : userId, Username = "test-user", DisplayName = "Test User",
                        RefreshToken = "upstream-new",
                        Integrations = new List<LinbikIntegrationToken> { new() { PackageName = "test-service", Token = "integration-new" } }
                    }
                })
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackRefreshAndLogoutUseLocalStore(bool paseto)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var linbik = builder.Services.AddLinbik(o =>
        {
            o.EnableHeartbeat = false;
            o.ServiceId = Guid.NewGuid().ToString();
            o.ApiKey = "test-only-unused-key";
        });
        if (paseto) linbik.AddLinbikPasetoAuth(o => o.PkceEnabled = false);
        else linbik.AddLinbikJwtAuth(o => o.PkceEnabled = false);
        builder.Services.AddLinbikRateLimiting();
        builder.Services.AddSingleton(DispatchProxy.Create<ILinbikAuthClient, LoginOnlyClient>());
        await using var app = builder.Build();
        app.UseRouting();
        app.UseLinbikRateLimiting();
        if (paseto) app.UseLinbikPasetoAuth();
        else app.UseLinbikJwtAuth();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var login = await client.GetAsync("/api/Linbik/callback?code=test-code");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var cookie = RefreshCookie(login);
        Assert.Contains("linbik-local-", cookie);
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/Linbik/refresh");
        refreshRequest.Headers.Add("Cookie", cookie);
        using var refresh = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = RefreshCookie(refresh);
        Assert.NotEqual(cookie, rotated);
        Assert.Contains(refresh.Headers.GetValues("Set-Cookie"), c => c.StartsWith(LinbikDefaults.AuthTokenCookie + "="));
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Get, "/api/Linbik/logout");
        logoutRequest.Headers.Add("Cookie", rotated);
        using var logout = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        using var rejectedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/Linbik/refresh");
        rejectedRequest.Headers.Add("Cookie", rotated);
        using var rejected = await client.SendAsync(rejectedRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    private static string RefreshCookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie")
        .Single(c => c.StartsWith(LinbikDefaults.RefreshTokenCookie + "=")).Split(';')[0];

    public class LoginOnlyClient : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.DoesNotContain("Refresh", targetMethod!.Name);
            return Task.FromResult<LinbikTokenResponse?>(new() { UserId = Guid.NewGuid(), Username = "test-user" });
        }
    }
}
