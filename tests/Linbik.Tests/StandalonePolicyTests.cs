using Linbik.Core;
using Linbik.Core.Attributes;
using Linbik.Core.Extensions;
using Linbik.Tests.Slices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Tests;

public sealed class StandalonePolicyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsAnotherSchemeInExplicitOrFallbackPolicy(bool fallback)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new ProbeState());
        builder.Services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, FakeSchemeHandler>(LinbikDefaults.DelegatedScheme, _ => { });
        var wrong = new AuthorizationPolicyBuilder(LinbikDefaults.ClientScheme).RequireAuthenticatedUser().Build();
        builder.Services.AddAuthorization(o => { if (fallback) o.FallbackPolicy = wrong; });
        await using var app = builder.Build();
        app.UseLinbikStandaloneFlows();
        app.UseAuthorization();
        app.MapLinbikStandaloneEndpoints(routes =>
        {
            var endpoint = routes.MapGet("/api/probe", () => "must not execute")
                .WithMetadata(new LFlowAuthorizeAttribute("Delegated"));
            if (!fallback) endpoint.RequireAuthorization(wrong);
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/delegated/probe");
        request.Headers.Add(FakeSchemeHandler.HeaderName, LinbikDefaults.DelegatedScheme);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));
        Assert.Contains("another authentication scheme", error.Message);
    }
}
