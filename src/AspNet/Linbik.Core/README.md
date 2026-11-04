# Linbik.Core

Core library for the Linbik Authentication Framework. Provides shared interfaces, models, configuration, and the OAuth 2.1 auth client.

## 📦 Installation

```bash
dotnet add package Linbik.Core
```

> **Not**: Bu paket genellikle `Linbik.JwtAuthManager` veya diğer Linbik paketleri tarafından otomatik olarak dahil edilir. Sadece manuel entegrasyon için doğrudan yükleyin.

## 🚀 Features

- **OAuth 2.1 Authorization Code Flow** with PKCE support
- **Multi-Service Integration** — Issue multiple JWT tokens in single response
- **Apps (Service-to-Service)** token operations
- **Keyless Mode** — Zero-configuration development
- **Heartbeat** — SDK-to-server health signals
- **Configuration Validation** — Startup-time validation with `IValidateOptions<T>`
- **HTTP Resilience** — Polly-based retry policies
- **Health Checks** — Built-in health check integration

## 🔧 Configuration

### Basic Setup (Fluent Builder)

```csharp
// In Program.cs
builder.Services.AddLinbik()
    .AddLinbikJwtAuth()      // optional: Linbik.JwtAuthManager (or AddLinbikPasetoAuth() from Linbik.PasetoAuthManager)
    .AddLinbikServer()       // optional: Linbik.Server
    .AddLinbikYarp();        // optional: Linbik.YARP

var app = builder.Build();
app.EnsureLinbik(); // Validates all registered Linbik modules at startup
```

### appsettings.json

```json
{
  "Linbik": {
    "LinbikUrl": "https://api.linbik.com",
    "Name": "MyApp",
    "KeylessMode": true,
    "ServiceId": "your-service-guid",
    "ApiKey": "lnbk_your_api_key",
    "Clients": [
      {
        "Name": "Default",
        "ClientId": "your-client-guid",
        "RedirectUrl": "https://yourapp.com",
        "ActionResultType": "Redirect"
      }
    ],
    "EnablePKCE": true,
    "AccessTokenLifetimeMinutes": 60,
    "RefreshTokenLifetimeDays": 30,
    "AppsTokenEndpoint": "/api/oauth/apps-token",
    "AppsTargetServices": {
      "payment-gateway": "target-service-guid"
    },
    "EnableHeartbeat": true,
    "HeartbeatIntervalSeconds": 60
  }
}
```

### Configuration Validation

Options are validated at startup. If configuration is invalid, the application will fail to start with a clear error message:

```
OptionsValidationException: Linbik:LinbikUrl is required.
OptionsValidationException: Linbik:ApiKey is required and cannot be empty.
```

## 📚 Main Interfaces

### IAuthService

Core authentication service for communicating with Linbik authorization server.

```csharp
public interface IAuthService
{
    Task<LinbikTokenResponse?> ExchangeCodeForTokensAsync(
        string code, CancellationToken cancellationToken = default);

    Task<UserProfile?> GetUserProfileAsync(
        HttpContext context, CancellationToken cancellationToken = default);

    Task<List<LinbikIntegrationToken>> GetIntegrationTokensAsync(
        HttpContext context, CancellationToken cancellationToken = default);

    Task<bool> RefreshTokensAsync(
        HttpContext context, CancellationToken cancellationToken = default);

    Task LogoutAsync(
        HttpContext context, CancellationToken cancellationToken = default);
}
```

### ILinbikAuthClient

HTTP client for Linbik authorization server communication.

```csharp
public interface ILinbikAuthClient
{
    // User-Context Token Operations
    Task<LBaseResponse<LinbikInitiateResponse>> InitiateAuthAsync(
        LinbikInitiateRequest request, CancellationToken cancellationToken = default);

    Task<LinbikTokenResponse?> ExchangeCodeAsync(
        string code, CancellationToken cancellationToken = default);

    Task<LinbikTokenResponse?> RefreshTokensAsync(
        string refreshToken, CancellationToken cancellationToken = default);

    // Apps (Service-to-Service) Token Operations
    Task<LinbikApplicationTokenResponse?> GetApplicationTokensAsync(
        LinbikApplicationTokenRequest request, CancellationToken cancellationToken = default);

    Task<LinbikApplicationTokenResponse?> GetApplicationTokensAsync(
        IEnumerable<string> targetPackageNames, CancellationToken cancellationToken = default);

    // Client Management
    Task<bool> UpdateClientRedirectUriByNameAsync(
        string clientName, string redirectUri, CancellationToken cancellationToken = default);
}
```

## 📋 Models

### LinbikTokenResponse

Response from token exchange and refresh endpoints.

```csharp
public sealed class LinbikTokenResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; }
    public string DisplayName { get; set; }
    public string? CodeChallenge { get; set; }
    public string? QueryParameters { get; set; }
    public List<LinbikIntegrationToken>? Integrations { get; set; }
    public string? RefreshToken { get; set; }
    public long? RefreshTokenExpiresAt { get; set; }
    public long? AccessTokenExpiresAt { get; set; }
    public Guid? ClientId { get; set; }
    public bool? Claimed { get; set; }       // Keyless Mode
    public string? NewApiKey { get; set; }   // Keyless Mode
}
```

### LinbikIntegrationToken

Per-service JWT token data.

```csharp
public sealed class LinbikIntegrationToken
{
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; }
    public string PackageName { get; set; }
    public string ServiceUrl { get; set; }
    public string Token { get; set; }         // JWT signed with service's private key
}
```

### UserProfile

User profile information extracted from cookies.

```csharp
public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public string Username { get; set; }
    public string DisplayName { get; set; }
    public Dictionary<string, string> IntegrationTokens { get; set; }
}
```

### Apps (Service-to-Service) Models

```csharp
public sealed class LinbikApplicationTokenRequest
{
    public Guid SourceServiceId { get; set; }
    public List<Guid> TargetServiceIds { get; set; } = [];
    public List<string> TargetPackageNames { get; set; } = [];
}

public sealed class LinbikApplicationTokenResponse
{
    public List<LinbikApplicationIntegration> Integrations { get; set; } = [];
    public long AccessTokenExpiresAt { get; set; }
}

public sealed class LinbikApplicationIntegration
{
    public string ServiceName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty; // PASETO/JWT signed with target service's private key
}
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.JwtAuthManager](../Linbik.JwtAuthManager/README.md)
- [Linbik.PasetoAuthManager](../Linbik.PasetoAuthManager/README.md)
- [Linbik.Server](../Linbik.Server/README.md)
- [Linbik.YARP](../Linbik.YARP/README.md)

## 📄 License

MIT License

**Contact**: info@linbik.com

---

**Version**: 1.2.0  
**Platform**: ASP.NET Core 10.0 (net10.0)  
**Last Updated**: 9 Eylül 2026

## Flow documents without a gateway

Register the three documents instead of a single AddOpenApi registration:

```csharp
using Linbik.Core.Extensions;

builder.Services.AddLinbikFlowDocuments();
// ...
app.MapOpenApi();
```

This exposes /openapi/self.json, /openapi/delegated.json and /openapi/apps.json.
The latter two match Linbik.YARP's default DelegatedDocumentPath and DocumentPath.
AddLinbikFlowExtension is included automatically. Existing endpoint paths are preserved.

Filtering uses LFlowAuthorizeAttribute metadata (also emitted by Linbik.Slices).
Self maps to self, Delegated to delegated, Application to apps. Multiple allowed flows
include an operation in multiple documents. No flow restriction, or an empty allowed-flow
list, includes the operation in all documents, matching the existing flow transformer.
Authentication attributes alone do not classify a document: specify flow metadata explicitly.

Optional per-document customization is available through AddLinbikFlowDocuments(options => ...).
If customizing ShouldInclude, compose the existing predicate to retain flow filtering.

No MapLinbikGatewayDocs or AddLinbikGatewayAuth is needed in a standalone application.
MapLinbikScalar belongs to the gateway example; it is optional UI, not document generation.
Use Scalar's MapScalarApiReference and register self/delegated/apps with AddDocument if desired.
Continue configuring the application's normal authentication and authorization.
MapOpenApi does not restrict document downloads by itself; apply the desired authorization
policy to its returned endpoint builder if documentation access must be restricted.

## Direct-host flow routing (no gateway)

Use this explicit mode only in the application that receives and authenticates the
external request. Do not enable it in a service receiving gateway identity headers.

```csharp
builder.Services.AddLinbikFlowDocuments();
// Register Self and Server authentication using the normal Linbik modules.

var app = builder.Build();
app.UseAuthentication();
app.UseLinbikStandaloneFlows(); // includes routing and verifies the selected scheme
app.UseAuthorization();

app.MapLinbikStandaloneEndpoints(routes =>
{
    routes.MapLinbikSlices(); // generated extension, import its namespace
    // Attribute-routed controllers may instead be mapped here with MapControllers().
});
app.MapOpenApi();
```

Map the business endpoints only inside this callback; do not also map their original
versions outside it. Keep login/callback/session/integration lifecycle endpoint mapping
outside this callback. Those system endpoints retain their own authentication rules.

An original /api/apps/delegated/payments endpoint becomes:
- Self: /api/apps/delegated/payments
- Delegated: /api/delegated/apps/delegated/payments
- Application: /api/apps/apps/delegated/payments

Allowed flows come from LFlowAuthorize metadata (emitted by slice flow attributes).
Unsupported flow routes are not created. Public "*" routes allow anonymous access;
missing or empty flow metadata requires authentication for the selected route.
Incoming Linbik-* headers are cleared. Only successful local authentication establishes
the trusted flow feature and internal flow header. Existing gateway mode is unchanged.
Do not put another UseAuthentication after UseLinbikStandaloneFlows: it could replace
the selected identity with the host's default identity. Continue normal authorization
for roles/permissions; avoid conflicting authentication scheme declarations.
OpenAPI documents select these concrete routes, rather than synthesizing documentation
for paths that do not exist. Existing standalone documents without this routing mode
continue to preserve their original paths.

OpenAPI schema IDs for nested slice Request/Response contracts and their
LBaseResponse<T> wrappers include the declaring type name. Regenerated clients
may therefore use new model names. AddLinbikFlowDocuments(configure: ...) runs
the configuration callback after the default naming is installed, so applications
can override CreateSchemaReferenceId there.
