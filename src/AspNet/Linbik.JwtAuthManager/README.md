# Linbik.JwtAuthManager

JWT Authentication Manager for Linbik Framework. Provides cookie-based login/logout endpoints, RSA-256 JWT validation, and rate limiting.

## 📦 Installation

```bash
dotnet add package Linbik.JwtAuthManager
```

## 🚀 Features

- **Cookie-Based JWT Auth** — Login, logout, and token refresh via minimal API endpoints
- **RSA-256 JWT Signing** — Industry-standard asymmetric cryptography
- **PKCE Support** — Proof Key for Code Exchange for public clients
- **Rate Limiting** — Configurable `LinbikAuth` and `LinbikAuthStrict` policies
- **Multi-Client Support** — Web, Mobile, Admin via `Clients` configuration
- **Keyless Mode** — Zero-config development with auto-provisioning
- **LinbikAuthorize Attribute** — Protect endpoints with `[LinbikAuthorize]`

## 🔧 Configuration

### Fluent Builder (Recommended)


```csharp
// In Program.cs
using Linbik.Core.Extensions;
using Linbik.JwtAuthManager.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Linbik services
builder.Services.AddLinbik()
    .AddLinbikJwtAuth();

var app = builder.Build();

// 2. Validate configuration at startup
app.EnsureLinbik();

// 3. if using controllers, add this.
app.UseRouting();  

// 4. Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// 5. Map endpoints: /api/linbik/login, /api/linbik/logout, /api/linbik/refresh
app.UseLinbikJwtAuth();

app.Run();
```

## 🔐 Configure JWT Authentication

Choose **one** of the following approaches:

```csharp
// Default
AddLinbikJwtAuth();

// From configuration
AddLinbikJwtAuth(builder.Configuration.GetSection("Linbik:JwtAuth"));

// Manual
AddLinbikJwtAuth(opt => { });
```
  
### Endpoints

| Path | Method | Description |
|------|--------|-------------|
| `/api/linbik/login` | GET | Initiate OAuth flow → exchange code → set cookies |
| `/api/linbik/logout` | POST | Clear all auth cookies |
| `/api/linbik/refresh` | POST | Refresh tokens using refresh cookie |
| `/api/linbik/callback` | GET | OAuth callback for code exchange |

## 💻 Usage

### Protect Endpoints

```csharp
[LinbikAuthorize]
[HttpGet]
public IActionResult Protected()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Ok(new { userId });
}
```
or with minimal APIs:
```csharp
app.MapGet("/protected", [LinbikAuthorize] (ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Results.Ok(new { userId });
});

app.MapGet("/protected", (HttpContext context) =>
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Results.Ok(new { userId });
})
.RequireAuthorization("LinbikAuthorize");
```

### Access Integration Tokens

```csharp
// Get a specific integration token from cookies
var paymentToken = HttpContext.GetIntegrationToken("payment-gateway");

// Check if user has any integrations
var hasIntegrations = HttpContext.HasIntegrations();
```

## 🔒 Rate Limiting

```csharp
// In Program.cs
builder.Services.AddLinbikRateLimiting();

// In middleware pipeline
app.UseLinbikRateLimiting();
```

```json
{
  "Linbik": {
    "RateLimiting": {
      "PermitLimit": 100,
      "WindowSeconds": 60,
      "QueueLimit": 2
    },
    "Resilience": {
      "StrictTokenLimit": 10,
      "StrictReplenishmentPeriodSeconds": 60,
      "StrictTokensPerPeriod": 5,
      "StrictQueueLimit": 0
    }
  }
}
```

### Rate Limiting Policies

| Policy | Use Case |
|--------|----------|
| `LinbikAuth` | Standard auth endpoints |
| `LinbikAuthStrict` | Token exchange, initiate endpoints |

```csharp
[EnableRateLimiting("LinbikAuth")]
public IActionResult RateLimitedAction() => Ok();
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.Core](../Linbik.Core/README.md)
- [Linbik.Server](../Linbik.Server/README.md)
- [Linbik.YARP](../Linbik.YARP/README.md)

## 📄 License

MIT License

**Contact**: info@linbik.com

---

**Version**: 1.2.0  
**Platform**: ASP.NET Core 10.0 (net10.0)  
**Last Updated**: 2 April 2026
