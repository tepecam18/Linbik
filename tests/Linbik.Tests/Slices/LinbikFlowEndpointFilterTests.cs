using Linbik.Slices.Endpoints;

namespace Linbik.Tests.Slices;

public sealed class LinbikFlowEndpointFilterTests
{
    private static readonly string[] SelfApp = ["Self", "Application"];

    [Fact]
    public async Task AllowedFlowHeaderReachesHandler()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikFlowEndpointFilter>(SelfApp);

        using var response = await host.SendAsync("Self");

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(1, host.State.HandlerCalls);
    }

    [Fact]
    public async Task ForbiddenFlowReturns403AndSkipsHandler()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikFlowEndpointFilter>(SelfApp);

        using var response = await host.SendAsync("Delegated");
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(403, body.StatusCode);
        Assert.False(body.IsSuccess);
        Assert.Equal("forbidden_flow", body.Title);
        Assert.Contains("Self, Application", body.Message);
        Assert.Equal(0, host.State.HandlerCalls);
    }

    [Fact]
    public async Task MissingHeaderReturns401AndSkipsHandler()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikFlowEndpointFilter>(SelfApp);

        using var response = await host.SendAsync();
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(401, body.StatusCode);
        Assert.False(body.IsSuccess);
        Assert.Equal("unauthorized", body.Title);
        Assert.Equal(0, host.State.HandlerCalls);
    }

    [Fact]
    public async Task DuplicateHeaderReturns401AndSkipsHandler()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikFlowEndpointFilter>(SelfApp);

        using var response = await host.SendAsync("Self", extraFlowHeaders: ["Self"]);
        var body = await FlowTestHost.ReadAsync(response);

        Assert.Equal(401, body.StatusCode);
        Assert.Contains("exactly once", body.Message);
        Assert.Equal(0, host.State.HandlerCalls);
    }

    [Fact]
    public async Task PublicMarkerAllowsRequestWithoutHeader()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikFlowEndpointFilter>(["*"]);

        using var response = await host.SendAsync();

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(1, host.State.HandlerCalls);
    }

    // Characterization, not a contract: without LFlowAuthorizeAttribute metadata the filter treats the
    // allowed list as empty and accepts any clean header. The generator always emits the metadata, so
    // this cannot happen for generated slices today.
    [Fact]
    public async Task EndpointWithoutMetadataAcceptsAnyCleanHeader()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikFlowEndpointFilter>(allowedFlows: null);

        using var response = await host.SendAsync("Anything");

        Assert.Equal(200, (int)response.StatusCode);
    }

    [Fact]
    public async Task EndpointWithoutMetadataStillRejectsMissingHeader()
    {
        await using var host = await FlowTestHost.StartAsync<LinbikFlowEndpointFilter>(allowedFlows: null);

        using var response = await host.SendAsync();

        Assert.Equal(401, (int)response.StatusCode);
        Assert.Equal(0, host.State.HandlerCalls);
    }
}
