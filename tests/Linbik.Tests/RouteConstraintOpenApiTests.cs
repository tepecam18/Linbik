using System.Net;
using System.Text.Json.Nodes;
using Linbik.Core.Attributes;
using Linbik.Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Tests;

public sealed class RouteConstraintOpenApiTests
{
    [Theory]
    [InlineData(":GUID", "12345678-1234-1234-1234-123456789abc", "string", "uuid")]
    [InlineData(":guid?", "", "string", "uuid")]
    [InlineData(":int=42", "", "integer", "int32")]
    [InlineData(":long", "2147483648", "integer", "int64")]
    [InlineData(":bool", "true", "boolean", null)]
    [InlineData(":decimal", "12.3456", "number", "decimal")]
    [InlineData(":double", "1.5", "number", "double")]
    [InlineData(":float", "1.5", "number", "float")]
    [InlineData(":datetime", "2030-01-01", "string", "date-time")]
    [InlineData(":int:min(1)", "42", "integer", "int32")]
    [InlineData(":regex(^[a-z]{{2}}$)", "ab", "string", null)]
    [InlineData("", "not-a-uuid", "string", null)]
    public async Task ConstraintsDescribePathValuesWithoutChangingStringBinding(
        string constraint, string value, string type, string? format)
    {
        var pattern = "/values/{value" + constraint + "}";
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLinbikFlowDocuments();
        await using var app = builder.Build();
        app.MapPost(pattern, ([FromRoute] string? value) => value ?? "missing")
            .WithMetadata(new LinbikRoutePatternMetadata(pattern));
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var doc = JsonNode.Parse(await client.GetStringAsync("/openapi/self.json"))!;
        var parameter = Assert.Single(doc["paths"]!["/values/{value}"]!["post"]!["parameters"]!.AsArray())!;
        Assert.Equal("path", parameter["in"]!.GetValue<string>());
        Assert.Equal(type, parameter["schema"]!["type"]!.GetValue<string>());
        Assert.Equal(format, parameter["schema"]!["format"]?.GetValue<string>());
        using var response = await client.PostAsync("/values/" + value, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(value.Length > 0 ? value : constraint.Contains('=') ? "42" : "missing",
            await response.Content.ReadAsStringAsync());
    }
}
