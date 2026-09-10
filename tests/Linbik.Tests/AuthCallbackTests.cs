using System.Net;
using System.Reflection;
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
        using var response = await client.GetAsync("/api/Linbik/callback");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Authorization code is required", await response.Content.ReadAsStringAsync());
        using var limitedResponse = await client.GetAsync("/api/Linbik/callback");
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
    }

    public class UnusedPasetoHelper : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("Missing-code callback must not invoke token operations.");
    }
}
