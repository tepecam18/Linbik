using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Linbik.Core.Configuration;
using Linbik.Core.Extensions;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Extensions;
using Linbik.PasetoAuthManager.Configuration;
using Linbik.PasetoAuthManager.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Linbik.Tests;

public sealed class AuthCallbackTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackWithoutCodeReturnsBadRequestForBothProviders(bool paseto)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.Configure<LinbikOptions>(_ => { });
        builder.Services.Configure<JwtAuthOptions>(_ => { });
        builder.Services.Configure<PasetoAuthOptions>(_ => { });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IAuditLogger, DefaultAuditLogger>();
        builder.Services.AddSingleton<LinbikMetrics>();
        builder.Services.AddHttpClient<ILinbikAuthClient, LinbikAuthClient>();
        builder.Services.AddSingleton(DispatchProxy.Create<IPasetoHelper, UnusedPasetoHelper>());
        builder.Services.AddLinbikRateLimiting(options =>
        {
            options.StrictTokenLimit = 1;
            options.StrictTokensPerPeriod = 1;
            options.StrictReplenishmentPeriodSeconds = 3600;
            options.StrictQueueLimit = 0;
        });
        await using var app = builder.Build();
        app.UseRouting();
        app.UseLinbikRateLimiting();
        if (paseto) app.UseLinbikPasetoAuth();
        else app.UseLinbikJwtAuth();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/linbik/callback");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Authorization code is required", await response.Content.ReadAsStringAsync());
        using var limitedResponse = await client.GetAsync("/api/linbik/callback");
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndpointsWorkWithoutExplicitAddLinbikRateLimitingCall(bool paseto)
    {
        // Regression test: AddLinbikJwtAuth/AddLinbikPasetoAuth endpoints unconditionally
        // RequireRateLimiting("LinbikAuth"/"LinbikStrict"). An app that wires up rate limiting
        // middleware (app.UseLinbikRateLimiting()) but never calls AddLinbikRateLimiting()
        // must not fail requests with "no such policy exists".
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IAuditLogger, DefaultAuditLogger>();
        builder.Services.AddSingleton<LinbikMetrics>();
        builder.Services.AddHttpClient<ILinbikAuthClient, LinbikAuthClient>();
        builder.Services.AddSingleton(DispatchProxy.Create<IPasetoHelper, UnusedPasetoHelper>());
        var linbik = builder.Services.AddLinbik(o => o.EnableHeartbeat = false);
        if (paseto) linbik.AddLinbikPasetoAuth(o => o.PkceEnabled = false);
        else linbik.AddLinbikJwtAuth(o => o.PkceEnabled = false);
        await using var app = builder.Build();
        app.UseRouting();
        app.UseLinbikRateLimiting();
        if (paseto) app.UseLinbikPasetoAuth();
        else app.UseLinbikJwtAuth();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/linbik/callback");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Authorization code is required", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredAuthorizationReturnsToConfiguredClientWithoutTokenExchange(bool paseto)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.Configure<LinbikOptions>(o => o.Clients.Add(new LinbikClientConfig
        { Name = "web", ClientId = Guid.NewGuid().ToString(), RedirectUrl = "/?tab=home#login" }));
        builder.Services.Configure<JwtAuthOptions>(_ => { });
        builder.Services.Configure<PasetoAuthOptions>(_ => { });
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IAuditLogger, DefaultAuditLogger>();
        builder.Services.AddSingleton<LinbikMetrics>();
        builder.Services.AddHttpClient<ILinbikAuthClient, LinbikAuthClient>();
        builder.Services.AddSingleton(DispatchProxy.Create<IPasetoHelper, UnusedPasetoHelper>());
        builder.Services.AddLinbikRateLimiting();
        await using var app = builder.Build();
        app.UseRouting();
        app.UseLinbikRateLimiting();
        if (paseto) app.UseLinbikPasetoAuth(); else app.UseLinbikJwtAuth();
        await app.StartAsync();
        var context = new DefaultHttpContext { RequestServices = app.Services };
        var state = AuthorizationRecovery.Save(context, "web");
        var cookie = context.Response.Headers.SetCookie.ToString().Split(';')[0];
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var forged = await client.GetAsync("/api/linbik/callback?error=authorization_expired&state=wrong");
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Empty(forged.Headers.TryGetValues("Set-Cookie", out var badCookies) ? badCookies : []);
        using var response = await client.GetAsync("/api/linbik/callback?error=authorization_expired&state=" + state);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/?tab=home&error=", response.Headers.Location!.OriginalString);
        Assert.Contains("error_code=authorization_expired", response.Headers.Location.OriginalString);
        Assert.EndsWith("#login", response.Headers.Location.OriginalString);
        var deleted = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(deleted, c => c.StartsWith("linbik_auth_recovery=;"));
        Assert.Contains(deleted, c => c.StartsWith("pkce_verifier=;"));
        Assert.DoesNotContain(deleted, c => c.StartsWith("authToken="));
    }

    public class UnusedPasetoHelper : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("Missing-code callback must not invoke token operations.");
    }
}
