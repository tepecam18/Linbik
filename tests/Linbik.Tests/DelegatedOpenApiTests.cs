using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using Linbik.Core.Extensions;
using Linbik.YARP.Configuration;
using Linbik.YARP.Extensions;
using Linbik.YARP.Interfaces;
using Linbik.YARP.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Linbik.Tests;

public sealed class DelegatedOpenApiTests
{
    [Theory]
    [InlineData("linbik-flows")]
    public void GatewayPreservesLinbikFlowName(string key)
    {
        var source = JsonNode.Parse("""
            {"openapi":"3.1.0","paths":{"/api/orders/list":{"get":{"FLOW_KEY":["Delegated"],"responses":{"200":{"description":"OK"}}}}}}
            """.Replace("FLOW_KEY", key))!;
        var snapshot = new ApiGateway.Docs.DownstreamOpenApiSnapshot("orders", "Orders", null,
            DateTimeOffset.UtcNow, source.ToJsonString(), source, DateTimeOffset.UtcNow);
        var result = JsonNode.Parse(ApiGateway.Docs.FilteredDocumentBuilder.Build([snapshot], "Delegated"))!;
        var operation = result["paths"]!["/api/delegated/orders/list"]!["get"]!;
        Assert.Equal("Delegated", operation["linbik-flows"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task UnavailableImportsLeaveHostFlowDocumentWorking()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IOptions<YARPOptions>>(Options.Create(Configuration()));
        builder.Services.AddSingleton(DispatchProxy.Create<IApplicationTokenProvider, HttpClientTests.FixedTokenProvider>());
        builder.Services.AddLinbikDelegatedOpenApi(o => o.CacheDirectory = null);
        builder.Services.AddSingleton<IHttpClientFactory>(new Factory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("not an OpenAPI document") }));
        builder.Services.AddOpenApi(o => o.AddLinbikFlowExtension().AddLinbikDelegatedDocuments());
        await using var app = builder.Build();
        app.MapGet("/local", () => "local");
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        Assert.Equal("*", document["paths"]!["/local"]!["get"]!["linbik-flows"]![0]!.GetValue<string>());
        Assert.Null(document["paths"]!["/api/messtick/delegated/orders"]);
        await app.StopAsync();
    }

    private const string Source = """
        {"openapi":"3.0.3","info":{"title":"Remote","version":"1"},
        "servers":[{"url":"https://remote.invalid"}],"security":[{"Bearer":[]}],
        "paths":{"/delegated/orders":{"get":{"linbik-flows":["Delegated"],"operationId":"getOrders","servers":[{"url":"https://remote.invalid"}],
        "responses":{"200":{"description":"OK","content":{"application/json":{"schema":{"$ref":"#/components/schemas/Order"}}}}}}}},
        "components":{"schemas":{"Order":{"type":"object","properties":{"id":{"type":"string"}}}},
        "securitySchemes":{"Bearer":{"type":"http","scheme":"bearer"}}}}
        """;

    [Theory]
    [InlineData("/api/delegated")]
    [InlineData("/api/delegated/")]
    public void TargetPrefixReplacementPreservesRootAndOperationReferences(string targetPath)
    {
        var source = JsonNode.Parse("""
            {"openapi":"3.1.0","paths":{
            "/api/delegated":{"get":{"responses":{"200":{"description":"OK"}}}},
            "/api/delegated/items":{"get":{"responses":{"200":{"description":"OK","links":{
            "root":{"operationRef":"#/paths/~1api~1delegated/get"}}}}}}}}
            """)!.AsObject();
        var merged = DelegatedDocumentMerger.Merge(new JsonObject(), source, "messtick", "/api/messtick/", targetPath);
        Assert.NotNull(merged["paths"]!["/api/messtick"]);
        Assert.NotNull(merged["paths"]!["/api/messtick/items"]);
        Assert.Equal("#/paths/~1api~1messtick/get", merged["paths"]!["/api/messtick/items"]!["get"]!["responses"]!["200"]!["links"]!["root"]!["operationRef"]!.GetValue<string>());
        var outside = JsonNode.Parse("""{"openapi":"3.1.0","paths":{"/api/delegated-other/items":{}}}""")!.AsObject();
        Assert.Throws<InvalidOperationException>(() => DelegatedDocumentMerger.Merge(new JsonObject(), outside, "messtick", "/api/messtick", targetPath));
    }

    [Fact]
    public void MergeSeparatesComponentsAndPreservesHostWhileUsingLocalProxy()
    {
        var host = JsonNode.Parse("""{"openapi":"3.1.0","info":{"title":"Host","version":"1"},"paths":{"/local":{}},"components":{"schemas":{"Order":{"type":"string"}}}}""")!.AsObject();
        var merged = DelegatedDocumentMerger.Merge(host, JsonNode.Parse(Source)!.AsObject(), "messtick", "/api/messtick");
        merged = DelegatedDocumentMerger.Merge(merged, JsonNode.Parse(Source)!.AsObject(), "other", "/api/other");
        Assert.Equal("Host", merged["info"]!["title"]!.GetValue<string>());
        Assert.Equal(3, merged["paths"]!.AsObject().Count);
        Assert.Equal(3, merged["components"]!["schemas"]!.AsObject().Count);
        var path = merged["paths"]!["/api/messtick/delegated/orders"]!;
        Assert.Equal("/", path["servers"]![0]!["url"]!.GetValue<string>());
        Assert.Null(path["get"]!["servers"]);
        Assert.NotEqual("getOrders", path["get"]!["operationId"]!.GetValue<string>());
        var reference = path["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.NotNull(merged["components"]!["schemas"]![reference.Split('/').Last()]);
        var scheme = path["get"]!["security"]![0]!.AsObject().Single().Key;
        Assert.NotNull(merged["components"]!["securitySchemes"]![scheme]);
        Assert.Single(host["paths"]!.AsObject());
        Assert.Throws<InvalidOperationException>(() => DelegatedDocumentMerger.Merge(merged, JsonNode.Parse(Source)!.AsObject(), "third", "/api/messtick"));
    }

    [Fact]
    public async Task AuthenticatedCacheRetainsSuccessfulDocumentAcrossFailureAndRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var status = HttpStatusCode.OK;
            var requests = new List<string?>();
            var factory = new Factory(request =>
            {
                requests.Add(request.Headers.Authorization?.ToString());
                Assert.Equal("https://remote.invalid/openapi/delegated.json", request.RequestUri!.AbsoluteUri);
                return new HttpResponseMessage(status) { Content = new StringContent(Source) };
            });
            DelegatedDocumentCache CreateCache() => new(factory,
                DispatchProxy.Create<IApplicationTokenProvider, HttpClientTests.FixedTokenProvider>(),
                Options.Create(Configuration()), Options.Create(new LinbikDelegatedOpenApiOptions { CacheDirectory = directory }),
                WebApplication.CreateBuilder().Environment, NullLogger<DelegatedDocumentCache>.Instance);
            using var cache = CreateCache();
            Assert.Equal(Source, (await cache.GetDocumentsAsync(default))["messtick"]);
            status = HttpStatusCode.Unauthorized;
            await cache.RefreshAsync(false, default);
            Assert.Equal(Source, (await cache.GetDocumentsAsync(default))["messtick"]);
            using var restarted = CreateCache();
            Assert.Equal(Source, (await restarted.GetDocumentsAsync(default))["messtick"]);
            Assert.Equal(3, requests.Count);
            Assert.All(requests, authorization => Assert.Equal("Bearer test-token", authorization));
            Assert.DoesNotContain("test-token", await File.ReadAllTextAsync(Directory.GetFiles(directory).Single()));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task HostOpenApiIncludesImportedOperationsAndProxyPreservesRemotePath(bool includeFlowExtension, bool openApiFirst)
    {
        var source = Source.Replace("/delegated/orders", "/api/delegated/integrations/settings");
        var outgoing = new List<(string Url, string? Authorization)>();
        var factory = new Factory(request =>
        {
            outgoing.Add((request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString()));
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(request.RequestUri.AbsolutePath.EndsWith(".json") ? source : "proxied") };
        });
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        void RegisterOpenApi() => builder.Services.AddOpenApi(o =>
        {
            if (includeFlowExtension) o.AddLinbikFlowExtension();
        });
        if (openApiFirst) RegisterOpenApi();
        new Linbik.Core.Builders.LinbikBuilder(builder.Services).AddLinbikYarp(o =>
        {
            o.IntegrationServices = Configuration().IntegrationServices;
            o.IntegrationServices["messtick"].DocumentPath = null;
            o.IntegrationServices["messtick"].TargetPath = "/api/delegated";
        });
        if (!openApiFirst) RegisterOpenApi();
        builder.Services.Configure<LinbikDelegatedOpenApiOptions>(o => o.CacheDirectory = null);
        builder.Services.AddSingleton(DispatchProxy.Create<IApplicationTokenProvider, HttpClientTests.FixedTokenProvider>());
        builder.Services.AddSingleton<IHttpClientFactory>(factory);
        await using var app = builder.Build();
        app.MapGet("/local", () => "local");
        app.MapOpenApi();
        app.UseLinbikYarp();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        Assert.NotNull(document["paths"]!["/local"]);
        Assert.NotNull(document["paths"]!["/api/messtick/integrations/settings"]);
        Assert.Null(document["paths"]!["/api/messtick/api/delegated/integrations/settings"]);
        Assert.Equal("Delegated", document["paths"]!["/api/messtick/integrations/settings"]!["get"]!["linbik-flows"]![0]!.GetValue<string>());
        if (includeFlowExtension)
            Assert.Equal("*", document["paths"]!["/local"]!["get"]!["linbik-flows"]![0]!.GetValue<string>());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/messtick/integrations/settings?page=2");
        request.Headers.Authorization = new("Bearer", "delegated-user-token");
        using var response = await client.SendAsync(request);
        Assert.Equal("proxied", await response.Content.ReadAsStringAsync());
        Assert.Contains(("https://remote.invalid/api/delegated/integrations/settings?page=2", "Bearer delegated-user-token"), outgoing);
        source = Source.Replace("/delegated/orders", "/api/delegated/invoices");
        await app.Services.GetRequiredService<DelegatedDocumentCache>().RefreshAsync(false, default);
        var updated = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        Assert.Null(updated["paths"]!["/api/messtick/integrations/settings"]);
        Assert.NotNull(updated["paths"]!["/api/messtick/invoices"]);
        Assert.NotNull(updated["paths"]!["/local"]);
        await app.StopAsync();
    }

    private static YARPOptions Configuration() => new()
    {
        IntegrationServices = new() { ["messtick"] = new() { SourcePath = "/api/messtick", TargetBaseUrl = "https://remote.invalid" } }
    };

    private sealed class Factory(Func<HttpRequestMessage, HttpResponseMessage> send) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(send));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }
}
