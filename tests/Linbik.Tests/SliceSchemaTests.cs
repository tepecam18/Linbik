using System.Text.Json.Nodes;
using Linbik.Core.Extensions;
using Linbik.Core.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Tests;

public sealed class SliceSchemaTests
{
    [Fact]
    public async Task WrappedSliceResponsesResolveToTheirOwnSchemas()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLinbikFlowDocuments();
        await using var app = builder.Build();
        app.MapGet("/create", () => new LBaseResponse<CreateBlock.Response>(new CreateBlock.Response("block")));
        app.MapGet("/draft", () => new LBaseResponse<GetDraft.Response>(new GetDraft.Response(1)));
        app.MapOpenApi();
        await app.StartAsync();

        using var client = app.GetTestClient();
        var doc = JsonNode.Parse(await client.GetStringAsync("/openapi/self.json"))!;
        var schemas = doc["components"]!["schemas"]!;
        Assert.NotNull(schemas["LBaseResponseOfSliceSchemaTestsCreateBlockResponse"]);
        Assert.NotNull(schemas["LBaseResponseOfSliceSchemaTestsGetDraftResponse"]);

        JsonNode Resolve(JsonNode schema)
        {
            if (schema["oneOf"] is JsonArray alternatives)
                schema = alternatives.Single(item => item?["$ref"] is not null)!;
            return schema["$ref"] is JsonNode reference
                ? schemas[reference.GetValue<string>().Split('/')[^1]]!
                : schema;
        }
        JsonNode DataProperties(string path)
        {
            var schema = doc["paths"]![path]!["get"]!["responses"]!["200"]!
                ["content"]!["application/json"]!["schema"]!;
            var data = Resolve(schema)["properties"]!["data"]!;
            return Resolve(data)["properties"]!;
        }

        Assert.NotNull(DataProperties("/create")["blockId"]);
        Assert.Null(DataProperties("/create")["revision"]);
        Assert.NotNull(DataProperties("/draft")["revision"]);
        Assert.Null(DataProperties("/draft")["blockId"]);
    }

    public static class CreateBlock
    {
        public sealed record Response(string BlockId);
    }

    public static class GetDraft
    {
        public sealed record Response(int Revision);
    }
}
