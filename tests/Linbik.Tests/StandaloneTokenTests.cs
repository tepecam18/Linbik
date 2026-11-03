using System.Net;
using System.Security.Claims;
using Linbik.Core;
using Linbik.Core.Attributes;
using Linbik.Core.Extensions;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Tests;

public sealed class StandaloneTokenTests
{
    [Theory]
    [InlineData("valid", 200)]
    [InlineData("audience", 401)]
    [InlineData("issuer", 401)]
    [InlineData("expired", 401)]
    [InlineData("signature", 401)]
    [InlineData("application", 401)]
    public async Task DelegatedRouteValidatesRealTokens(string scenario, int expected)
    {
        var helper = new PasetoHelperService();
        var keys = helper.GenerateKeyPair();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IPasetoHelper>(helper);
        builder.Services.AddAuthentication().AddLinbikPasetoBearer(LinbikDefaults.DelegatedScheme, o =>
        {
            o.PublicKey = keys.PublicKeyBase64;
            o.ExpectedAudience = "test";
            o.ExpectedIssuer = scenario == "issuer" ? "other" : "Linbik";
            o.RequireApplicationToken = false;
        });
        await using var app = builder.Build();
        app.UseLinbikStandaloneFlows();
        app.MapLinbikStandaloneEndpoints(routes =>
            routes.MapGet("/api/payments/{id:int}", (int id) => id)
                .WithMetadata(new LFlowAuthorizeAttribute("Delegated")));
        await app.StartAsync();
        var token = await helper.CreateTokenAsync(
            [new Claim("sub", "user"), new Claim("token_type", scenario == "application" ? "apps" : "user")],
            scenario == "signature" ? helper.GenerateKeyPair().PrivateKeyBase64 : keys.PrivateKeyBase64,
            scenario == "audience" ? "other" : "test", scenario == "expired" ? -60 : 5);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/delegated/payments/1");
        request.Headers.Authorization = new("Bearer", token);
        request.Headers.Add("Linbik-Flow", "Delegated");
        Assert.Equal((HttpStatusCode)expected, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/delegated/payments/no-number")).StatusCode);
        await app.StopAsync();
    }
}
