# Linbik.Server

Server-side repository interfaces, JWT validation, and authentication middleware for Linbik integration services.

## 🚀 Features

### Authorization Server (v2.0+)
- **Authorization Code Management** - Generate, validate, and consume codes
- **Refresh Token Management** - Issue and revoke refresh tokens
- **User Profile Management** - Retrieve user profile data
- **Service Integration** - Manage service-to-service relationships
- **User Consent Management** - Track user permissions for services
- **Repository Pattern** - Clean abstraction for data access

### Integration Service Features (v2.1+)
- **Dual JWT Authentication Schemes** - `LinbikUserService` (user context) + `LinbikS2S` (machine context)
- **[LinbikUserServiceAuthorize]** - RS256 JWT attribute for user-context endpoints
- **[LinbikS2SAuthorize]** - RS256 JWT attribute for service-to-service endpoints
- **Role-Based S2S Authorization** - `[LinbikS2SAuthorize("Linbik")]` (platform) / `[LinbikS2SAuthorize("Service")]` (service)
- **Cross-Scheme Injection Protection** - `OnTokenValidated` events prevent token misuse between schemes
- **IntegrationTokenValidator** - RSA public key based JWT validation
- **LinbikTokenClaims** - Strongly typed claims for user identity
- **HttpContext Extensions** - Easy access to validated claims

### Health Checks & Observability (v2.4+)
- **Health Check Endpoints** - `/health`, `/ready`, `/live` for Kubernetes probes
- **OpenTelemetry Integration** - Distributed tracing and metrics
- **Automatic ASP.NET Core Instrumentation** - HTTP and client instrumentation
- **OTLP Export** - Export to Jaeger, Zipkin, Grafana Tempo

### Legacy Features (Deprecated)
- Simple login validation (use authorization code flow)
- Basic app validation (use service validation)

## 📦 Installation

```bash
dotnet add package Linbik.Server
```

## 🔧 Configuration

### Basic Setup (Authorization Server)

```csharp
services.AddLinbikServer(options =>
{
    options.ServerUrl = "https://linbik.com";
    options.AuthorizationEndpoint = "/auth/{serviceId}/{codeChallenge?}";
    options.TokenEndpoint = "/oauth/token";
    options.RefreshEndpoint = "/oauth/refresh";
    options.ConsentEndpoint = "/auth/consent";
    options.RequireHttps = true;
    options.RequirePKCE = false;  // Optional but recommended
});
```

### Integration Service Setup (JWT Validation)

For services that receive integration tokens from main services:

```csharp
// In Program.cs

// 1. Add services with public key configuration (fluent chain)
builder.Services.AddLinbik(builder.Configuration)
    .AddLinbikServer(options =>
    {
        options.ServiceId = "your-service-guid";
        options.PublicKey = builder.Configuration["Linbik:PublicKey"];
        options.ClockSkewMinutes = 5;
        options.ValidateAudience = true;
        options.ValidateIssuer = true;
    });

var app = builder.Build();

// 2. Validate configuration at startup
app.EnsureLinbik();

// 3. Use standard ASP.NET Core middleware
app.UseAuthentication();
app.UseAuthorization();

// 3. Protect endpoints with attributes
[ApiController]
public class PaymentController : ControllerBase
{
    // User-context endpoint
    [LinbikUserServiceAuthorize]
    [HttpPost("/charge")]
    public IActionResult Charge([FromBody] ChargeRequest request)
    {
        var claims = HttpContext.GetLinbikClaims();
        var userId = HttpContext.GetLinbikUserId();
        return Ok();
    }

    // S2S endpoint — any S2S token
    [LinbikS2SAuthorize]
    [HttpPost("/s2s/sync")]
    public IActionResult SyncData() => Ok();

    // S2S — service tokens only (role=Service)
    [LinbikS2SAuthorize("Service")]
    [HttpPost("/s2s/webhook/{eventType}")]
    public IActionResult S2SWebhook(string eventType) => Ok();

    // S2S — platform tokens only (role=Linbik)
    [LinbikS2SAuthorize("Linbik")]
    [HttpPost("/s2s/platform-event")]
    public IActionResult OnPlatformEvent() => Ok();
}
```

### ServerOptions Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServiceId` | `string` | null | Your integration service ID (for audience validation) |
| `PublicKey` | `string` | null | RSA public key (PEM or Base64 format) |
| `ClockSkewMinutes` | `int` | 5 | Allowed clock skew for token validation |
| `ValidateAudience` | `bool` | true | Validate JWT audience claim |
| `ValidateIssuer` | `bool` | true | Validate JWT issuer claim |

## 🏥 Health Checks (v2.4+)

Linbik.Server provides built-in health check endpoints for Kubernetes and container orchestration:

### Setup

```csharp
// In Program.cs

// 1. Add health checks
builder.Services.AddLinbikHealthChecks(options =>
{
    options.Enabled = true;
    options.HealthPath = "/health";
    options.ReadyPath = "/ready";
    options.LivePath = "/live";
    options.IncludeDetails = builder.Environment.IsDevelopment();
});

// 2. Map health check endpoints
app.UseLinbikHealthChecks();
```

### Configuration (appsettings.json)

```json
{
  "Linbik": {
    "HealthChecks": {
      "Enabled": true,
      "HealthPath": "/health",
      "ReadyPath": "/ready",
      "LivePath": "/live",
      "IncludeDetails": false
    }
  }
}
```

### Endpoints

| Endpoint | Purpose | Kubernetes Probe |
|----------|---------|------------------|
| `/health` | General health status | - |
| `/ready` | Is service ready to accept traffic? | readinessProbe |
| `/live` | Is service alive? | livenessProbe |

### Response Example

```json
{
  "status": "Healthy",
  "totalDuration": 12.5,
  "timestamp": "2026-02-02T10:30:00Z",
  "checks": [
    {
      "name": "linbik_auth",
      "status": "Healthy",
      "duration": 5.2,
      "description": "Linbik authentication is configured and ready."
    }
  ]
}
```

## 📊 OpenTelemetry (v2.4+)

Distributed tracing and metrics support for observability:

### Setup

```csharp
// In Program.cs

builder.Services.AddLinbikTelemetry(options =>
{
    options.ServiceName = "payment-gateway";
    options.ServiceVersion = "1.0.0";
    options.EnableTracing = true;
    options.EnableMetrics = true;
    options.OtlpEndpoint = "http://jaeger:4317";  // Or any OTLP collector
    options.EnableConsoleExporter = builder.Environment.IsDevelopment();
});
```

### Configuration (appsettings.json)

```json
{
  "Linbik": {
    "Telemetry": {
      "EnableTracing": true,
      "EnableMetrics": true,
      "ServiceName": "my-integration-service",
      "ServiceVersion": "1.0.0",
      "OtlpEndpoint": "http://localhost:4317",
      "OtlpProtocol": "grpc",
      "EnableConsoleExporter": false,
      "TraceSampleRatio": 1.0,
      "EnableAspNetCoreInstrumentation": true,
      "EnableHttpClientInstrumentation": true
    }
  }
}
```

### Features

- **Automatic ASP.NET Core Instrumentation** - HTTP request/response tracing
- **HTTP Client Instrumentation** - Outgoing HTTP call tracing
- **Runtime Metrics** - GC, memory, thread pool metrics
- **OTLP Export** - Send to Jaeger, Zipkin, Grafana Tempo, etc.
- **Console Export** - Development debugging

### Custom Tracing

```csharp
using System.Diagnostics;

public class PaymentService
{
    private static readonly ActivitySource ActivitySource = 
        new("Linbik.Server");

    public async Task<PaymentResult> ProcessPayment(PaymentRequest request)
    {
        using var activity = ActivitySource.StartActivity("ProcessPayment");
        activity?.SetTag("payment.amount", request.Amount);
        activity?.SetTag("payment.currency", request.Currency);
        
        // Process payment...
        
        activity?.SetTag("payment.transaction_id", result.TransactionId);
        return result;
    }
}
```

## 📚 Main Interfaces

### ILinbikServerRepository

Complete repository interface for authorization operations. See full documentation at [Full Documentation](https://github.com/tepecam18/Linbik).

## 🔐 JWT Validation (Integration Services)

### IntegrationTokenValidator

Validates integration JWT tokens using RSA public key.

### LinbikTokenClaims Model

Strongly typed claims extracted from validated JWT tokens.

### HttpContext Extensions

Easy access to validated Linbik claims:

```csharp
var userId = HttpContext.GetLinbikUserId();
var userName = HttpContext.GetLinbikUserName();
var nickName = HttpContext.GetLinbikNickName();
var isAuthenticated = HttpContext.HasLinbikUser();
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.Core](../Linbik.Core/README.md)
- [Linbik.JwtAuthManager](../Linbik.JwtAuthManager/README.md)
- [Linbik.YARP](../Linbik.YARP/README.md)

## 📄 License

This library is currently a work in progress and is not ready for production use.

**Contact**: info@linbik.com

---

**Version**: 2.4.0  
**Last Updated**: 28 Şubat 2026
