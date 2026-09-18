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
    [InlineData("PUT", "/admin", "admin_changed", true, "2026-09-14T08:11:26.708793Z", "OnIntegrationAdminChangedAsync", "Integration admin updated")]
    [InlineData("PUT", "/status", "toggled", false, "2026-09-14T08:12:30.6160607Z", "OnIntegrationToggledAsync", "Integration status updated")]
    [InlineData("DELETE", "", "removed", false, "2026-09-14T08:13:12.6359959Z", "OnIntegrationRemovedAsync", "Integration removed")]
    public async Task LifecyclePayloadPreservesAllFields(string method, string suffix, string eventType,
        bool enabled, string timestamp, string callback, string message)
    {
        var json = $$"""
            {"event_type":"{{eventType}}","integration_id":"5b2801a8-17d0-462c-a30e-5eaec8e1e839","service":{"service_id":"fc639334-9315-450a-864b-abea2e86a8c7","service_name":"Leptudo.Api","package_name":"dev-leptudoapi-160f5079","base_url":"https://localhost:53930"},"admin_profile":{"profile_id":"019e6e4d-8b4a-73af-b10b-c76bc5cc9cd8","username":"tpcm","display_name":"Mustafa T."},"is_enabled":{{enabled.ToString().ToLowerInvariant()}},"timestamp":"{{timestamp}}"}
            """;
        var recorder = new RecordingHandler();
        await using var app = await CreateApp(recorder);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method),
            "/events/5b2801a8-17d0-462c-a30e-5eaec8e1e839" + suffix)
        { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(callback, recorder.LastCallback);
        var expected = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var actual = System.Text.Json.JsonSerializer.SerializeToNode(recorder.LastEvent)!;
        // Compare every supplied field, including nested profile/service data and event time.
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(expected, actual), actual.ToJsonString());
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        Assert.True(body!["success"]!.GetValue<bool>());
        Assert.Equal(message, body["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task MalformedDeleteBodyDoesNotInvokeHandlerWithEmptyData()
    {
        var recorder = new RecordingHandler();
        await using var app = await CreateApp(recorder);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/events/5b2801a8-17d0-462c-a30e-5eaec8e1e839")
        { Content = new StringContent("{invalid", System.Text.Encoding.UTF8, "application/json") };
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(recorder.LastEvent);
    }

    [Fact]
    public async Task CreatedNotificationBindsPlatformSnakeCasePayload()
    {
        const string json = """
            {"event_type":"created","integration_id":"5b2801a8-17d0-462c-a30e-5eaec8e1e839","service":{"service_id":"fc639334-9315-450a-864b-abea2e86a8c7","service_name":"Leptudo.Api","package_name":"dev-leptudoapi-160f5079","base_url":"https://localhost:53930"},"admin_profile":{"profile_id":"2c86f21f-2f24-47bc-994a-b0539ea5cb45","username":"test","display_name":"deneme"},"is_enabled":true,"timestamp":"2026-09-14T08:04:04.7648197Z"}
            """;
        var recorder = new RecordingHandler();
        await using var app = await CreateApp(recorder);
        using var client = app.GetTestClient();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/events", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var value = Assert.IsType<IntegrationEvent>(recorder.LastEvent);
        Assert.Equal(IntegrationEventType.Created, value.EventType);
        Assert.Equal(Guid.Parse("5b2801a8-17d0-462c-a30e-5eaec8e1e839"), value.IntegrationId);
        Assert.Equal(Guid.Parse("fc639334-9315-450a-864b-abea2e86a8c7"), value.Service.ServiceId);
        Assert.Equal("Leptudo.Api", value.Service.ServiceName);
        Assert.Equal("dev-leptudoapi-160f5079", value.Service.PackageName);
        Assert.Equal("https://localhost:53930", value.Service.BaseUrl);
        Assert.Equal(Guid.Parse("2c86f21f-2f24-47bc-994a-b0539ea5cb45"), value.AdminProfile.ProfileId);
        Assert.Equal("test", value.AdminProfile.Username);
        Assert.Equal("deneme", value.AdminProfile.DisplayName);
        Assert.True(value.IsEnabled);
        Assert.Equal(DateTime.Parse("2026-09-14T08:04:04.7648197Z", null,
            System.Globalization.DateTimeStyles.RoundtripKind), value.Timestamp);
    }

    [Theory]
    [InlineData("created", IntegrationEventType.Created)]
    [InlineData("removed", IntegrationEventType.Removed)]
    [InlineData("toggled", IntegrationEventType.Toggled)]
    [InlineData("admin_changed", IntegrationEventType.AdminChanged)]
    [InlineData("service_changed", IntegrationEventType.ServiceChanged)]
    public void PlatformEventNamesDeserialize(string name, IntegrationEventType expected)
    {
        var value = System.Text.Json.JsonSerializer.Deserialize<IntegrationEvent>(
            "{\"event_type\":\"" + name + "\"}");
        Assert.Equal(expected, value!.EventType);
    }

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
        public string? LastCallback { get; private set; }
        public bool Fail { get; init; }
        public bool Throws { get; init; }
        private Task<IntegrationEventResult> Record(IntegrationEvent value,
            [System.Runtime.CompilerServices.CallerMemberName] string? callback = null)
        {
            LastEvent = value;
            LastCallback = callback;
            if (Throws) throw new InvalidOperationException("Test failure");
            return Task.FromResult(Fail ? IntegrationEventResult.Failure("rejected") : IntegrationEventResult.Success());
        }
        public override Task<IntegrationEventResult> OnIntegrationCreatedAsync(IntegrationEvent value) => Record(value);
        public override Task<IntegrationEventResult> OnIntegrationRemovedAsync(IntegrationEvent value) => Record(value);
        public override Task<IntegrationEventResult> OnIntegrationToggledAsync(IntegrationEvent value) => Record(value);
        public override Task<IntegrationEventResult> OnIntegrationAdminChangedAsync(IntegrationEvent value) => Record(value);
    }
}
