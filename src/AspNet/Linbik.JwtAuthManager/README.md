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
builder.Services.AddLinbik(builder.Configuration.GetSection("Linbik"))
    .AddLinbikJwtAuth();

var app = builder.Build();
app.EnsureLinbik();

// Map endpoints: /linbik/login, /linbik/logout, /linbik/refresh
app.UseLinbikJwtAuth();
```

### Standalone Setup

```csharp
builder.Services.AddLinbikJwtAuth(builder.Configuration.GetSection("Linbik"));
```

### Endpoints

| Path | Method | Description |
|------|--------|-------------|
| `/linbik/login` | GET | Initiate OAuth flow → exchange code → set cookies |
| `/linbik/logout` | POST | Clear all auth cookies |
| `/linbik/refresh` | POST | Refresh tokens using refresh cookie |

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
builder.Services.AddLinbikRateLimiting(builder.Configuration.GetSection("Linbik"));

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
**Last Updated**: 2 Nisan 2026
