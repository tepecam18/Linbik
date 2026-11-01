using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Linbik.Core.Attributes;
using Linbik.Slices;
using Linbik.Slices.Endpoints;
using Linbik.Slices.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Linbik.Tests.Slices;

public sealed class LinbikSliceTestingTests
{
    // Stands in for a production handler that is already registered under a Linbik scheme name.
    private sealed class AlwaysFailingHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.Fail("production handler"));
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(
        bool registerProductionDelegated = false, bool addTestAuthentication = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        if (registerProductionDelegated)
            builder.Services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, AlwaysFailingHandler>("LinbikDelegated", null);

        if (addTestAuthentication)
            builder.Services.AddLinbikTestAuthentication();

        var app = builder.Build();

        app.MapGet("/auth", (HttpContext context) => Results.Ok(new { user = context.User.FindFirst("sub")?.Value }))
            .AddEndpointFilter<LinbikAuthorizeFlowEndpointFilter>()
            .WithMetadata(new LFlowAuthorizeAttribute("Self", "Delegated"));

        app.MapGet("/gateway", () => Results.Ok(new { reached = true }))
            .AddEndpointFilter<LinbikFlowEndpointFilter>()
            .WithMetadata(new LFlowAuthorizeAttribute("Self"));

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static async Task<string?> UserAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("user", out var user) ? user.GetString() : null;
    }

    [Theory]
    [InlineData(LinbikFlow.Self)]
    [InlineData(LinbikFlow.Delegated)]
    public async Task AsAuthenticatedPassesAuthorizeFlowSliceWithGivenUserId(LinbikFlow flow)
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var response = await client.AsAuthenticated(flow, "user-42").GetAsync("/auth");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("user-42", await UserAsync(response));
    }

    [Fact]
    public async Task AsAuthenticatedUsesDefaultUserId()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var response = await client.AsAuthenticated(LinbikFlow.Self).GetAsync("/auth");

        Assert.Equal(LinbikSliceTestingExtensions.DefaultUserId, await UserAsync(response));
    }

    [Fact]
    public async Task AsAuthenticatedWithFlowNotAllowedBySliceIsRejected()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var response = await client.AsAuthenticated(LinbikFlow.Application).GetAsync("/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousClientIsRejectedByAuthorizeFlowSlice()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var response = await client.GetAsync("/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddLinbikTestAuthenticationReplacesAlreadyRegisteredScheme()
    {
        var (app, client) = await StartAsync(registerProductionDelegated: true);
        await using var _ = app;

        using var response = await client.AsAuthenticated(LinbikFlow.Delegated, "u1").GetAsync("/auth");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("u1", await UserAsync(response));
    }

    [Fact]
    public async Task ProductionHandlerStaysInChargeWithoutTestAuthentication()
    {
        var (app, client) = await StartAsync(registerProductionDelegated: true, addTestAuthentication: false);
        await using var _ = app;

        using var response = await client.AsAuthenticated(LinbikFlow.Delegated).GetAsync("/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AsFlowSendsGatewayHeaderForFlowSlice()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var allowed = await client.AsFlow(LinbikFlow.Self).GetAsync("/gateway");
        using var forbidden = await client.AsFlow(LinbikFlow.Delegated).GetAsync("/gateway");

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AsFlowReplacesPreviousValueInsteadOfDuplicating()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        client.AsFlow(LinbikFlow.Delegated).AsFlow(LinbikFlow.Self);
        using var response = await client.GetAsync("/gateway");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AsAnonymousClearsFlowAndAuthenticationHeaders()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        client.AsFlow(LinbikFlow.Self).AsAuthenticated(LinbikFlow.Self).AsAnonymous();

        using var gateway = await client.GetAsync("/gateway");
        using var auth = await client.GetAsync("/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, gateway.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, auth.StatusCode);
    }

    [Fact]
    public void AsAuthenticatedRejectsBlankUserId()
    {
        using var client = new HttpClient();

        Assert.Throws<ArgumentException>(() => client.AsAuthenticated(LinbikFlow.Self, " "));
    }

    [Fact]
    public void AsAuthenticatedRejectsUndefinedFlow()
    {
        using var client = new HttpClient();

        Assert.Throws<ArgumentOutOfRangeException>(() => client.AsAuthenticated((LinbikFlow)99));
    }
}
