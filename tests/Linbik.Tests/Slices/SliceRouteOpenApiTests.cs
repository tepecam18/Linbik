using System.Runtime.Loader;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Linbik.Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Tests.Slices;

public sealed class SliceRouteOpenApiTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task GeneratedBodyEndpointsExposePathParametersAndKeepJsonBody(string method)
    {
        var source = SliceCompilation.Slice("[LFlowPublic]", method: method,
            pattern: "/orders/{request:guid}/lines/{line-id:int}")
            .Replace("record Request()", "record Request(decimal Amount, decimal? Discount, double Ratio)");
        var run = SliceCompilation.RunGenerator(source, "RouteFixture" + Guid.NewGuid().ToString("N"));
        Assert.Empty(run.CompilationErrors);
        using var stream = new MemoryStream();
        var emit = run.Output.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        stream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        var registry = assembly.GetType("Linbik.Slices.Generated.LinbikSlicesRegistry")!;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLinbikFlowDocuments();
        registry.GetMethod("AddLinbikSlices")!.Invoke(null, [builder.Services]);
        await using var app = builder.Build();
        registry.GetMethod("MapLinbikSlices")!.Invoke(null, [app]);
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();
        foreach (var flow in new[] { "self", "delegated", "apps" })
        {
            var doc = JsonNode.Parse(await client.GetStringAsync($"/openapi/{flow}.json"))!;
            var operation = doc["paths"]!["/orders/{request}/lines/{line-id}"]![method.ToLowerInvariant()]!;
            var parameters = operation["parameters"]!.AsArray();
            Assert.Equal(2, parameters.Count);
            foreach (var name in new[] { "request", "line-id" })
            {
                var parameter = Assert.Single(parameters, p => p!["name"]!.GetValue<string>() == name)!;
                Assert.Equal("path", parameter["in"]!.GetValue<string>());
                Assert.True(parameter["required"]!.GetValue<bool>());
                Assert.Equal(name == "request" ? "uuid" : "int32", parameter["schema"]!["format"]?.GetValue<string>());
            }
            var schema = operation["requestBody"]!["content"]!["application/json"]!["schema"]!;
            if (schema["$ref"] is { } reference)
                schema = doc["components"]!["schemas"]![reference.GetValue<string>().Split('/')[^1]]!;
            var properties = schema["properties"]!;
            Assert.Equal("decimal", properties["amount"]!["format"]!.GetValue<string>());
            Assert.Equal("decimal", properties["discount"]!["format"]!.GetValue<string>());
            Assert.Equal("double", properties["ratio"]!["format"]!.GetValue<string>());
            Assert.Null(properties["request"]);
            Assert.Null(properties["line-id"]);
        }
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/orders/{Guid.NewGuid()}/lines/42")
        { Content = JsonContent.Create(new { amount = 12.34m, discount = (decimal?)null, ratio = 1.5 }) };
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/files/{**path}", "path")]
    [InlineData("/items/{id?}", "id")]
    [InlineData("/items/{id=all}", "id")]
    [InlineData("/items/{code:regex(^[a-z]{{2}}$)}", "code")]
    [InlineData("/items/{{literal}}/{id}", "id")]
    public void RouteSyntaxProducesCompilableNamedBindings(string pattern, string name)
    {
        var run = SliceCompilation.RunGenerator(SliceCompilation.Slice("[LFlowPublic]", method: "POST", pattern: pattern));
        Assert.Empty(run.CompilationErrors);
        Assert.Contains($"FromRoute(Name = \"{name}\")", run.Generated);
        Assert.DoesNotContain("__linbikRoute1", run.Generated);
    }
}
