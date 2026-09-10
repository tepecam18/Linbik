using System.Net;
using System.Net.Http.Json;
using Linbik.Server.Extensions;
using Linbik.Server.Interfaces;
using Linbik.Server.Models;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Linbik.Tests;

public sealed class IntegrationEndpointTests
{
    [Theory]
    [InlineData("POST", "", IntegrationEventType.Created)]
    [InlineData("DELETE", "/{id}", IntegrationEventType.Removed)]
    [InlineData("PUT", "/{id}/status", IntegrationEventType.Toggled)]
    [InlineData("PUT", "/{id}/admin", IntegrationEventType.AdminChanged)]
    public async Task RoutesDispatchCorrectEventAndPreserveResponse(string method, string suffix, IntegrationEventType eventType)
    {
        var recorder = new RecordingHandler();
        await using var app = await CreateApp(recorder);
        using var client = app.GetTestClient();
        var id = Guid.NewGuid();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/events" + suffix.Replace("{id}", id.ToString()));
        if (method != "DELETE") request.Content = JsonContent.Create(new IntegrationEvent { IntegrationId = id });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(recorder.LastEvent);
        Assert.Equal(id, recorder.LastEvent.IntegrationId);
        Assert.Equal(eventType, recorder.LastEvent.EventType);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        Assert.True(body!["success"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(false, 400)]
    [InlineData(true, 500)]
    public async Task HandlerFailuresBecomeControlledResponses(bool throws, int expected)
    {
        await using var app = await CreateApp(new RecordingHandler { Fail = true, Throws = throws });
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/events", new IntegrationEvent());
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Fact]
    public async Task GatewayRoutesRejectRequestsWithoutFlowHeaders()
    {
        var recorder = new RecordingHandler();
        await using var app = await CreateApp(recorder, gateway: true);
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/events", new IntegrationEvent());
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(recorder.LastEvent);
    }

    private static async Task<WebApplication> CreateApp(RecordingHandler handler, bool gateway = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ILinbikIntegrationHandler>(handler);
        var app = builder.Build();
        if (gateway) app.MapLinbikIntegrationEndpointsGateway("/events");
        else app.MapLinbikIntegrationEndpointsAnonymous("/events");
        await app.StartAsync();
        return app;
    }

    private sealed class RecordingHandler : LinbikIntegrationHandler
    {
        public IntegrationEvent? LastEvent { get; private set; }
        public bool Fail { get; init; }
        public bool Throws { get; init; }
        private Task<IntegrationEventResult> Record(IntegrationEvent value)
        {
            LastEvent = value;
            if (Throws) throw new InvalidOperationException("Test failure");
            return Task.FromResult(Fail ? IntegrationEventResult.Failure("rejected") : IntegrationEventResult.Success());
        }
        public override Task<IntegrationEventResult> OnIntegrationCreatedAsync(IntegrationEvent value) => Record(value);
        public override Task<IntegrationEventResult> OnIntegrationRemovedAsync(IntegrationEvent value) => Record(value);
        public override Task<IntegrationEventResult> OnIntegrationToggledAsync(IntegrationEvent value) => Record(value);
        public override Task<IntegrationEventResult> OnIntegrationAdminChangedAsync(IntegrationEvent value) => Record(value);
    }
}
