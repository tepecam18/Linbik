using System.Net;
using System.Text.Json.Nodes;
using Linbik.Core;
using Linbik.Core.Attributes;
using Linbik.Core.Extensions;
using Linbik.Tests.Slices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Tests;

public sealed class StandaloneFlowTests
{
    [Fact]
    public async Task RoutesAuthenticateOnlyTheirSchemeAndPublishActualPaths()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new ProbeState());
        var auth = builder.Services.AddAuthentication();
        foreach (var scheme in new[] { LinbikDefaults.ClientScheme, LinbikDefaults.DelegatedScheme, LinbikDefaults.ApplicationScheme })
            auth.AddScheme<AuthenticationSchemeOptions, FakeSchemeHandler>(scheme, _ => { });
        builder.Services.AddLinbikFlowDocuments();
        await using var app = builder.Build();
        app.UseLinbikStandaloneFlows();
        app.MapLinbikStandaloneEndpoints(routes =>
        {
            routes.MapGet("/api/apps/delegated/payments", (HttpContext c) =>
                c.User.Identity!.AuthenticationType!)
                .WithMetadata(new LFlowAuthorizeAttribute("Self", "Delegated", "Application"));
            routes.MapGet("/api/self-only", () => "ok")
                .WithMetadata(new LFlowAuthorizeAttribute("Self"));
            routes.MapGet("/api/public", () => "ok")
                .WithMetadata(new LFlowAuthorizeAttribute("*"));
        });
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var rows = new[]
        {
            (Path: "/api/apps/delegated/payments", Scheme: LinbikDefaults.ClientScheme, Doc: "self"),
            (Path: "/api/delegated/apps/delegated/payments", Scheme: LinbikDefaults.DelegatedScheme, Doc: "delegated"),
            (Path: "/api/apps/apps/delegated/payments", Scheme: LinbikDefaults.ApplicationScheme, Doc: "apps")
        };
        foreach (var row in rows)
        {
            foreach (var scheme in rows.Select(r => r.Scheme).Append(""))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, row.Path);
                if (scheme.Length > 0) request.Headers.Add(FakeSchemeHandler.HeaderName, scheme);
                request.Headers.Add("Linbik-Flow", "Application");
                request.Headers.Add("Linbik-UserId", "spoof");
                using var response = await client.SendAsync(request);
                Assert.Equal(scheme == row.Scheme ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
                if (scheme == row.Scheme)
                    Assert.Equal(scheme, await response.Content.ReadAsStringAsync());
            }
            var document = JsonNode.Parse(await client.GetStringAsync($"/openapi/{row.Doc}.json"))!;
            Assert.NotNull(document["paths"]![row.Path]);
            foreach (var other in rows.Where(r => r.Doc != row.Doc))
                Assert.Null(document["paths"]![other.Path]);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/delegated/self-only")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/public")).StatusCode);
        await app.StopAsync();
    }
}
