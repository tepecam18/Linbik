using System.Text.Json.Nodes;
using Linbik.Core.Attributes;
using Linbik.Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Tests;

public sealed class FlowDocumentTests
{
    [Theory]
    [InlineData("self", "/self", "/apps")]
    [InlineData("delegated", "/delegated", "/self")]
    [InlineData("apps", "/apps", "/delegated")]
    public async Task DocumentsFilterByFlowWithoutGateway(string document, string included, string excluded)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLinbikFlowDocuments();
        await using var app = builder.Build();
        app.MapGet("/self", () => "ok").WithMetadata(new LFlowAuthorizeAttribute("Self"));
        app.MapGet("/delegated", () => "ok").WithMetadata(new LFlowAuthorizeAttribute("Delegated"));
        app.MapGet("/apps", () => "ok").WithMetadata(new LFlowAuthorizeAttribute("Application"));
        app.MapGet("/shared", () => "ok").WithMetadata(new LFlowAuthorizeAttribute("Self", "Delegated"));
        app.MapGet("/authenticated", () => "ok").WithMetadata(new LFlowAuthorizeAttribute());
        app.MapGet("/public", () => "ok");
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var json = JsonNode.Parse(await client.GetStringAsync($"/openapi/{document}.json"))!;
        var paths = json["paths"]!;
        Assert.NotNull(paths[included]);
        Assert.Null(paths[excluded]);
        Assert.NotNull(paths["/public"]);
        Assert.NotNull(paths["/authenticated"]);
        Assert.Equal(document != "apps", paths["/shared"] is not null);
        Assert.NotNull(paths[included]!["get"]!["linbik-flows"]);
        await app.StopAsync();
    }
}
