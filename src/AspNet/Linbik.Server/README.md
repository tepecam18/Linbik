# Linbik.Server

Integration service package for Linbik Framework. Provides PASETO v4.public (Ed25519) validation middleware, dual authentication schemes (delegated user context + application/service-to-service), integration lifecycle handlers, telemetry, and health checks.

## 📦 Installation

```bash
dotnet add package Linbik.Server
```

## 🚀 Features

- **Dual PASETO Authentication Schemes** — `LinbikDelegated` (`LinbikDefaults.DelegatedScheme`, user context) + `LinbikApplication` (`LinbikDefaults.ApplicationScheme`, machine/service-to-service context), both validated against the same Ed25519 `PublicKey`
- **[LinbikDelegatedAuthorize]** — attribute for user-context endpoints (rejects application tokens)
- **[LinbikApplicationAuthorize]** — attribute for application/service-to-service endpoints, with optional role-based access (rejects user tokens)
- **Cross-Scheme Injection Protection** — `PasetoBearerHandler`'s per-scheme `RequireApplicationToken` flag prevents token misuse between schemes
- **ILinbikIntegrationHandler** — Integration lifecycle events (created, removed, toggled, admin changed)
- **OpenTelemetry** — Built-in telemetry with `AddLinbikTelemetry()`
- **Health Checks** — Service health monitoring with `AddLinbikHealthChecks()`

## 🔧 Configuration

### Fluent Builder (Recommended)

```csharp
// In Program.cs
builder.Services.AddLinbik()
    .AddLinbikServer();

var app = builder.Build();
app.EnsureLinbik();

app.UseAuthentication();
app.UseAuthorization();
```

### Standalone Setup

```csharp
builder.Services.Configure<LinbikOptions>(options =>
{
    options.PackageName = "payment-gateway";
    options.PublicKey = "..."; // Base64 Ed25519 public key
    options.JwtIssuer = "Linbik";
});
builder.Services.AddLinbikServer();
```

> Validation schemes are always registered. In keyless mode provisioning supplies the shared key and audience. Standard mode requires Linbik:PublicKey and Linbik:PackageName.

## 💻 Usage

### Protecting Endpoints

```csharp
[ApiController]
[Route("api/integration")]
public class IntegrationController : ControllerBase
{
    // User-context endpoint (requires delegated PASETO token)
    [LinbikDelegatedAuthorize]
    [HttpPost("charge")]
    public IActionResult Charge([FromBody] ChargeRequest request)
    {
        var userId = HttpContext.User.FindFirst("sub")?.Value;
        return Ok();
    }

    // Application endpoint — any application/service-to-service token accepted
    [LinbikApplicationAuthorize]
    [HttpPost("app/sync")]
    public IActionResult SyncData() => Ok();

    // Application endpoint — only service-to-service tokens (role=Service)
    [LinbikApplicationAuthorize("Service")]
    [HttpPost("app/webhook/{eventType}")]
    public IActionResult AppWebhook(string eventType) => Ok();

    // Application endpoint — only platform tokens (role=Linbik)
    [LinbikApplicationAuthorize("Linbik")]
    [HttpPost("app/platform-event")]
    public IActionResult OnPlatformEvent() => Ok();
}
```

### Reading Claims

`PasetoBearerHandler` builds the `ClaimsPrincipal` directly from the token's raw claim keys (e.g. `sub`, `name`, `preferred_username`, `azp`, `role`) — they are **not** mapped to `System.Security.Claims.ClaimTypes` constants:

```csharp
var userId = HttpContext.User.FindFirst("sub")?.Value;
var username = HttpContext.User.FindFirst("preferred_username")?.Value;
var isAuthenticated = HttpContext.User.Identity?.IsAuthenticated ?? false;
var roles = HttpContext.User.FindAll("role").Select(c => c.Value);
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
builder.Services.AddLinbikHealthChecks();

app.UseLinbikHealthChecks();
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.Core](../Linbik.Core/README.md)
- [Linbik.JwtAuthManager](../Linbik.JwtAuthManager/README.md)
- [Linbik.PasetoAuthManager](../Linbik.PasetoAuthManager/README.md)
- [Linbik.YARP](../Linbik.YARP/README.md)
- [Linbik.Slices](../Linbik.Slices/README.md)

## 📄 License

MIT License

**Contact**: info@linbik.com

---

**Version**: 1.2.0  
**Platform**: ASP.NET Core 10.0 (net10.0)  
**Last Updated**: 9 Eylül 2026


### Keyless receiving services

With `KeylessMode = true`, register `.AddLinbikServer()` to receive application and delegated PASETO tokens without manually configuring a public key. The server module provisions credentials after the HTTP server starts, including for services that never initiate a user login. Configure a client `RedirectUrl` with the public application URL when running behind a proxy; otherwise the listening address is used for initial provisioning.

Linbik generates and stores the Ed25519 private key; only the public key and package name are cached locally with the service credentials. Old credential caches missing this material are completed using the existing service's API key, preserving the service ID. Update the Linbik API first: this requires the `POST /api/dev/{serviceId}/validation-key` recovery endpoint and the new provisioning response fields.

An enabled integration authorizes apps tokens in both directions. Each caller uses its own API key and requests a separate token for its target; a token cannot be reused in the opposite direction. Disabling the integration prevents new token issuance; already issued tokens remain valid until expiry.

### Shared validation key

ServerOptions.PublicKey has been removed. Configure the receiving service's validation
key only through Linbik:PublicKey (LinbikOptions.PublicKey).
Both Delegated/Application authentication and IntegrationTokenValidator read this value.
In keyless mode provisioning populates the same option; no separate Server key is needed.
Move any existing Linbik:Server:PublicKey value to Linbik:PublicKey and remove the old entry.
Self session signing/encryption settings remain independent.

### Shared identity settings and removed legacy options

ServerOptions.PackageName and ServerOptions.JwtIssuer were removed along with PublicKey.
Use Linbik:PackageName, Linbik:JwtIssuer and Linbik:PublicKey only.
ServerOptions now contains IntegrationEndpointPath only.
Removed inert legacy options: AuthorizationCodeLifetimeMinutes, AccessTokenExpiration,
RefreshTokenExpirationDays, EnablePKCE, EnableIpWhitelisting and ClockSkewMinutes.
These options did not configure the receiving PASETO validator.
ValidateAudience and ValidateIssuer were also removed: incoming token audience and issuer
validation are mandatory. Core token lifetime/PKCE settings keep their existing meaning;
they do not change the lifetime of tokens issued by a remote service.