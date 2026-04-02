# Linbik.Server

Integration service package for Linbik Framework. Provides JWT validation middleware, dual authentication schemes (user + S2S), integration lifecycle handlers, telemetry, and health checks.

## 📦 Installation

```bash
dotnet add package Linbik.Server
```

## 🚀 Features

- **Dual JWT Authentication Schemes** — `LinbikUserService` (user context) + `LinbikS2S` (machine context)
- **[LinbikUserServiceAuthorize]** — RS256 JWT attribute for user-context endpoints
- **[LinbikS2SAuthorize]** — RS256 JWT attribute for S2S endpoints with role-based access
- **Cross-Scheme Injection Protection** — Prevents token misuse between schemes
- **ILinbikIntegrationHandler** — Integration lifecycle events (created, removed, toggled, admin changed)
- **OpenTelemetry** — Built-in telemetry with `AddLinbikTelemetry()`
- **Health Checks** — Service health monitoring with `AddLinbikHealthChecks()`

## 🔧 Configuration

### Fluent Builder (Recommended)

```csharp
// In Program.cs
builder.Services.AddLinbik(builder.Configuration.GetSection("Linbik"))
    .AddLinbikServer();

var app = builder.Build();
app.EnsureLinbik();

app.UseAuthentication();
app.UseAuthorization();
```

### Standalone Setup

```csharp
builder.Services.AddLinbikServer(options =>
{
    options.ServiceId = "your-service-guid";
    options.PublicKey = builder.Configuration["Linbik:PublicKey"];
    options.PackageName = "payment-gateway";
});
```

## 💻 Usage

### Protecting Endpoints

```csharp
[ApiController]
[Route("api/integration")]
public class IntegrationController : ControllerBase
{
    // User-context endpoint (requires user JWT)
    [LinbikUserServiceAuthorize]
    [HttpPost("charge")]
    public IActionResult Charge([FromBody] ChargeRequest request)
    {
        var claims = HttpContext.GetLinbikClaims();
        var userId = HttpContext.GetLinbikUserId();
        return Ok();
    }

    // S2S endpoint — any S2S token accepted
    [LinbikS2SAuthorize]
    [HttpPost("s2s/sync")]
    public IActionResult SyncData() => Ok();

    // S2S endpoint — only service-to-service tokens (role=Service)
    [LinbikS2SAuthorize("Service")]
    [HttpPost("s2s/webhook/{eventType}")]
    public IActionResult S2SWebhook(string eventType) => Ok();

    // S2S endpoint — only platform tokens (role=Linbik)
    [LinbikS2SAuthorize("Linbik")]
    [HttpPost("s2s/platform-event")]
    public IActionResult OnPlatformEvent() => Ok();
}
```

### HttpContext Extensions

```csharp
var claims = HttpContext.GetLinbikClaims();
var userId = HttpContext.GetLinbikUserId();
var userName = HttpContext.GetLinbikUserName();
var nickName = HttpContext.GetLinbikNickName();
var isAuthenticated = HttpContext.HasLinbikUser();
```

## 🔌 Integration Lifecycle Handler

Handle events when main services create/remove/toggle integrations with your service:

```csharp
public class MyIntegrationHandler : ILinbikIntegrationHandler
{
    public Task<IntegrationEventResult> OnIntegrationCreatedAsync(IntegrationEvent e)
    {
        // A new service wants to use our integration
        return Task.FromResult(IntegrationEventResult.Success());
    }

    public Task<IntegrationEventResult> OnIntegrationRemovedAsync(IntegrationEvent e)
        => Task.FromResult(IntegrationEventResult.Success());

    public Task<IntegrationEventResult> OnIntegrationToggledAsync(IntegrationEvent e)
        => Task.FromResult(IntegrationEventResult.Success());

    public Task<IntegrationEventResult> OnIntegrationAdminChangedAsync(IntegrationEvent e)
        => Task.FromResult(IntegrationEventResult.Success());
}
```

Register:

```csharp
// With custom handler
builder.Services.AddLinbikIntegrationHandler<MyIntegrationHandler>();

// Or default (no-op) handler
builder.Services.AddLinbikIntegrationHandler();

// Map integration endpoints
app.MapLinbikIntegrationEndpoints();
```

## 📊 Telemetry (OpenTelemetry)

```csharp
// Add OpenTelemetry tracing and metrics
builder.Services.AddLinbikTelemetry(options =>
{
    options.ServiceName = "payment-gateway";
    options.EnableConsoleExporter = true;
});
```

## 🏥 Health Checks

```csharp
builder.Services.AddLinbikHealthChecks(builder.Configuration.GetSection("Linbik"));

app.UseLinbikHealthChecks();
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.Core](../Linbik.Core/README.md)
- [Linbik.JwtAuthManager](../Linbik.JwtAuthManager/README.md)
- [Linbik.YARP](../Linbik.YARP/README.md)

## 📄 License

MIT License

**Contact**: info@linbik.com

---

**Version**: 1.2.0  
**Platform**: ASP.NET Core 10.0 (net10.0)  
**Last Updated**: 2 Nisan 2026
