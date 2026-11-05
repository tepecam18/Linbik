using Linbik.Core;
using Linbik.Core.Attributes;
using Linbik.Core.Extensions;
using Linbik.Slices;
using Linbik.Slices.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Tests.Slices;

public sealed class SliceStartupValidationTests
{
    private static WebApplicationBuilder Builder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLinbikSender();
        return builder;
    }

    [Fact]
    public async Task LocalAuthenticationWithoutSchemesFailsBeforeStartupSucceeds()
    {
        var builder = Builder();
        await using var app = builder.Build();
        app.MapGet("/api/private", () => "never called")
            .WithMetadata(new LFlowAuthorizeAttribute("Self"), new LinbikSliceAuthenticationMetadata());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
        Assert.Contains("/api/private", error.Message);
        Assert.Contains(LinbikDefaults.ClientScheme, error.Message);
        Assert.Contains("before builder.Build()", error.Message);
        Assert.Contains("AddLinbikPasetoAuth", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeaderBasedOrPublicSlicesDoNotRequireLocalAuthentication(bool isPublic)
    {
        var builder = Builder();
        await using var app = builder.Build();
        var endpoint = app.MapGet("/api/probe", () => "ok")
            .WithMetadata(new LFlowAuthorizeAttribute(isPublic ? "*" : "Self"));
        if (isPublic) endpoint.WithMetadata(new LinbikSliceAuthenticationMetadata());
        await app.StartAsync();
    }

    [Fact]
    public async Task LocalMultiFlowOnlyNeedsOneAllowedScheme()
    {
        var builder = Builder();
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FakeSchemeHandler>(LinbikDefaults.DelegatedScheme, _ => { });
        await using var app = builder.Build();
        app.MapGet("/api/multi", () => "ok").WithMetadata(
            new LFlowAuthorizeAttribute("Self", "Delegated"), new LinbikSliceAuthenticationMetadata());
        await app.StartAsync();
    }

    [Fact]
    public async Task StandaloneMissingMiddlewareFailsAtStartupEvenForPublicEndpoints()
    {
        var builder = Builder();
        await using var app = builder.Build();
        app.MapLinbikStandaloneEndpoints(r => r.MapGet("/api/probe", () => "ok")
            .WithMetadata(new LFlowAuthorizeAttribute("*")));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
        Assert.Contains("UseLinbikStandaloneFlows is missing", error.Message);
        Assert.Contains("app.UseAuthentication(); app.UseLinbikStandaloneFlows(); app.UseAuthorization();", error.Message);
    }

    [Fact]
    public async Task MappingBeforeMiddlewareRegistrationStillWorks()
    {
        var builder = Builder();
        await using var app = builder.Build();
        app.MapLinbikStandaloneEndpoints(r => r.MapGet("/api/probe", () => "ok")
            .WithMetadata(new LFlowAuthorizeAttribute("*")));
        app.UseLinbikStandaloneFlows();
        await app.StartAsync();
    }

    [Fact]
    public async Task StandaloneRequiresEveryPublishedProtectedFlowScheme()
    {
        var builder = Builder();
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FakeSchemeHandler>(LinbikDefaults.ClientScheme, _ => { });
        await using var app = builder.Build();
        app.UseLinbikStandaloneFlows();
        app.MapLinbikStandaloneEndpoints(r => r.MapGet("/api/probe", () => "ok")
            .WithMetadata(new LFlowAuthorizeAttribute("Self", "Application")));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
        Assert.Contains("/api/apps/probe", error.Message);
        Assert.Contains(LinbikDefaults.ApplicationScheme, error.Message);
        Assert.Contains("AddLinbikServer", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictingExplicitOrFallbackPolicyFailsAtStartup(bool fallback)
    {
        var builder = Builder();
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FakeSchemeHandler>(LinbikDefaults.DelegatedScheme, _ => { });
        var wrong = new AuthorizationPolicyBuilder(LinbikDefaults.ClientScheme).RequireAuthenticatedUser().Build();
        builder.Services.AddAuthorization(o => { if (fallback) o.FallbackPolicy = wrong; });
        await using var app = builder.Build();
        app.UseLinbikStandaloneFlows();
        app.UseAuthorization();
        app.MapLinbikStandaloneEndpoints(r =>
        {
            var endpoint = r.MapGet("/api/probe", () => "ok").WithMetadata(new LFlowAuthorizeAttribute("Delegated"));
            if (!fallback) endpoint.RequireAuthorization(wrong);
        });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
        Assert.Contains("another authentication scheme", error.Message);
        Assert.Contains("Remove AuthenticationSchemes", error.Message);
        Assert.Contains("UseAuthorization", error.Message);
    }

    [Fact]
    public async Task DuplicateStandaloneMiddlewareFailsWithOneCallExample()
    {
        var builder = Builder();
        await using var app = builder.Build();
        app.UseLinbikStandaloneFlows();
        var error = Assert.Throws<InvalidOperationException>(() => app.UseLinbikStandaloneFlows());
        Assert.Contains("registered twice", error.Message);
        Assert.Contains("Keep one call", error.Message);
    }
    [Fact]
    public async Task GeneratedHeadSliceAcceptsHeadAndRejectsPost()
    {
        var run = SliceCompilation.RunGenerator(SliceCompilation.Slice("[LFlowPublic]", method: "HEAD"),
            "HeadFixture" + Guid.NewGuid().ToString("N"));
        Assert.Empty(run.CompilationErrors);
        using var stream = new MemoryStream();
        Assert.True(run.Output.Emit(stream).Success);
        stream.Position = 0;
        var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(stream);
        var registry = assembly.GetType("Linbik.Slices.Generated.LinbikSlicesRegistry")!;
        var builder = Builder();
        registry.GetMethod("AddLinbikSlices")!.Invoke(null, [builder.Services]);
        await using var app = builder.Build();
        registry.GetMethod("MapLinbikSlices")!.Invoke(null, [app]);
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, "/demo");
        using var headResponse = await client.SendAsync(headRequest);
        Assert.Equal(System.Net.HttpStatusCode.OK, headResponse.StatusCode);
        using var postResponse = await client.PostAsync("/demo", null);
        Assert.Equal(System.Net.HttpStatusCode.MethodNotAllowed, postResponse.StatusCode);
    }

}
