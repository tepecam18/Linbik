using System.Net;
using System.Reflection;
using Linbik.Core.Configuration;
using Linbik.Core.Extensions;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Linbik.Tests;

public sealed class RateLimitMiddlewareCheckTests
{
    [Fact]
    public async Task NoUseRateLimiterMeansNoEnforcementAndNoCrash()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IAuditLogger, DefaultAuditLogger>();
        builder.Services.AddSingleton<LinbikMetrics>();
        builder.Services.AddHttpClient<ILinbikAuthClient, LinbikAuthClient>();
        var linbik = builder.Services.AddLinbik(o => o.EnableHeartbeat = false);
        linbik.AddLinbikJwtAuth(o => o.PkceEnabled = false);
        await using var app = builder.Build();
        app.UseRouting();
        // Deliberately NOT calling app.UseLinbikRateLimiting() / app.UseRateLimiter()
        app.UseLinbikJwtAuth();
        await app.StartAsync();
        using var client = app.GetTestClient();
        for (int i = 0; i < 20; i++)
        {
            using var response = await client.GetAsync("/api/Linbik/callback");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
