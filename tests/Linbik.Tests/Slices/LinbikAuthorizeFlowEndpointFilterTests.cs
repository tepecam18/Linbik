using Linbik.Slices.Endpoints;

namespace Linbik.Tests.Slices;

public sealed class LinbikAuthorizeFlowEndpointFilterTests
{
    private const string Self = "LinbikScheme";
    private const string Delegated = "LinbikDelegated";
    private const string Application = "LinbikApplication";

    private static readonly string[] AllSchemes = [Self, Delegated, Application];

    [Fact]
    public async Task AuthenticatedAllowedFlowPopulatesUserAndReachesHandler()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            ["Self"], AllSchemes);

        using var response = await host.SendAsync(authSchemes: [Self]);
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(200, body.StatusCode);
        Assert.Equal(Self, body.User);
        Assert.Equal(1, host.State.HandlerCalls);
    }

    [Fact]
    public async Task IdentityFromDisallowedFlowIsRejected()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            ["Delegated"], AllSchemes);

        using var response = await host.SendAsync(authSchemes: [Self]);
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(401, body.StatusCode);
        Assert.Equal("unauthorized", body.Title);
        Assert.Equal(0, host.State.HandlerCalls);
        Assert.Equal([Delegated], host.State.AuthenticateCalls);
    }

    [Fact]
    public async Task StopsAtFirstSuccessfulFlow()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            ["Self", "Delegated"], AllSchemes);

        using var response = await host.SendAsync(authSchemes: [Self, Delegated]);
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(200, body.StatusCode);
        Assert.Equal(Self, body.User);
        Assert.Equal([Self], host.State.AuthenticateCalls);
    }

    [Fact]
    public async Task NoSuccessfulFlowReturns401ListingVerifiedFlows()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            ["Self", "Application"], AllSchemes);

        using var response = await host.SendAsync();
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(401, body.StatusCode);
        Assert.False(body.IsSuccess);
        Assert.Equal("unauthorized", body.Title);
        Assert.Contains("Identity could not be verified for flow(s): Self, Application", body.Message);
        Assert.DoesNotContain("not configured", body.Message);
        Assert.Equal(0, host.State.HandlerCalls);
    }

    [Fact]
    public async Task UnregisteredSchemeIsSkippedWithoutThrowing()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            ["Self", "Delegated"], [Self]);

        using var response = await host.SendAsync();
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(401, body.StatusCode);
        Assert.Contains("Identity could not be verified for flow(s): Self", body.Message);
        Assert.Contains("Delegated", body.Message);
        Assert.Contains("not configured on this service", body.Message);
        Assert.Equal([Self], host.State.AuthenticateCalls);
    }

    [Fact]
    public async Task RegisteredSchemeStillAuthenticatesWhenAnotherAllowedSchemeIsMissing()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            ["Delegated", "Self"], [Self]);

        using var response = await host.SendAsync(authSchemes: [Self]);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(1, host.State.HandlerCalls);
    }

    [Fact]
    public async Task NoRegisteredSchemeAtAllReturns401WithConfigurationMessage()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            ["Self"], registeredSchemes: []);

        using var response = await host.SendAsync();
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(401, body.StatusCode);
        Assert.Contains("not configured on this service", body.Message);
        Assert.Equal(0, host.State.HandlerCalls);
    }

    [Fact]
    public async Task MissingAuthenticationServicesReturns401()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            ["Self"], registeredSchemes: null);

        using var response = await host.SendAsync();
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(401, body.StatusCode);
        Assert.Equal("unauthorized", body.Title);
        Assert.Contains("Authentication is not configured", body.Message);
        Assert.Equal(0, host.State.HandlerCalls);
    }

    [Fact]
    public async Task UnknownFlowNameIsTreatedAsNotConfigured()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            ["Bogus"], AllSchemes);

        using var response = await host.SendAsync(authSchemes: [Self, Delegated, Application]);
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(401, body.StatusCode);
        Assert.Contains("Bogus", body.Message);
        Assert.Contains("not configured on this service", body.Message);
        Assert.Empty(host.State.AuthenticateCalls);
        Assert.Equal(0, host.State.HandlerCalls);
    }

    // Characterization, not a contract: without LFlowAuthorizeAttribute metadata every flow is tried,
    // so any authenticated identity passes. Generated slices always carry the metadata.
    [Fact]
    public async Task EndpointWithoutMetadataTriesAllFlows()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            allowedFlows: null, AllSchemes);

        using var response = await host.SendAsync(authSchemes: [Application]);
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(200, body.StatusCode);
        Assert.Equal(Application, body.User);
        Assert.Equal([Self, Delegated, Application], host.State.AuthenticateCalls);
    }

    [Fact]
    public async Task EndpointWithoutMetadataRejectsAnonymousRequest()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikAuthorizeFlowEndpointFilter>(
            allowedFlows: null, AllSchemes);

        using var response = await host.SendAsync();

        Assert.Equal(401, (int)response.StatusCode);
        Assert.Equal(0, host.State.HandlerCalls);
    }
}
