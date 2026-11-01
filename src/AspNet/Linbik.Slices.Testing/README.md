# Linbik.Slices.Testing

Test helpers for services built with [Linbik.Slices](../Linbik.Slices/README.md). **For test projects only** — the fake authentication handlers trust a request header.

```csharp
public sealed class OrderTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient CreateClient() => factory
        .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddLinbikTestAuthentication()))
        .CreateClient();

    [Fact]
    public async Task GetOrder_AsSelfUser_Succeeds()
    {
        var client = CreateClient().AsAuthenticated(LinbikFlow.Self, userId: "42");   // [LAuthorizeFlow]

        var response = await client.GetAsync("/orders/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GatewayOnlySlice_RejectsUnlistedFlow()
    {
        var client = CreateClient().AsFlow(LinbikFlow.Delegated);                      // [LFlow(LinbikFlow.Self)]

        var response = await client.GetAsync("/orders/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

| Helper | Use for | Effect |
|---|---|---|
| `services.AddLinbikTestAuthentication()` | `ConfigureTestServices` | Replaces or adds `LinbikScheme`, `LinbikDelegated`, `LinbikApplication` with header-driven fake handlers |
| `client.AsFlow(flow)` | `[LFlow]` slices | Sends `Linbik-Flow: <flow>` like the Gateway would |
| `client.AsAuthenticated(flow, userId)` | `[LAuthorizeFlow]` slices | Authenticates on the flow's scheme; `sub` claim = `userId` |
| `client.AsAnonymous()` | any | Clears the headers set above |

Never reference this package from production projects.
