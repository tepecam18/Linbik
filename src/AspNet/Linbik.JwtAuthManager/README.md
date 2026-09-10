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
- **Rate Limiting** — Configurable `LinbikAuth`, `LinbikGeneral`, and `LinbikStrict` policies
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
// Default configuration
AddLinbikJwtAuth();

// From configuration
AddLinbikJwtAuth(builder.Configuration.GetSection("Linbik:JwtAuth"));

// Fluent configuration
AddLinbikJwtAuth(opt => { });
```
  
### Endpoints

| Path | Method | Description |
|------|--------|-------------|
| `/api/Linbik/login` | GET | Initiate OAuth flow → exchange code → set cookies |
| `/api/Linbik/callback` | GET | OAuth callback for code exchange |
| `/api/Linbik/logout` | GET | Clear all auth cookies |
| `/api/Linbik/refresh` | POST | Refresh tokens using refresh cookie |

> ⚠️ Login/callback/logout/refresh error paths currently crash with a `NotImplementedException` instead of returning a response — see `PROJECT_STATUS.md` Known Issues. `Linbik.PasetoAuthManager`'s equivalent handler is the reference fix.

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

Rate limiting is shared through `Linbik.Core.Extensions`. All overloads of `AddLinbikRateLimiting`, including the parameterless overload, register the policies.

```csharp
// In Program.cs
builder.Services.AddLinbikRateLimiting(builder.Configuration.GetSection("Linbik:RateLimiting"));

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
| `LinbikAuth` | Login and logout endpoints (fixed/sliding window) |
| `LinbikStrict` | Callback and refresh endpoints — token exchange (token-bucket) |
| `LinbikGeneral` | Registered for general-purpose use in your own endpoints (more permissive fixed window) |

```csharp
[EnableRateLimiting("LinbikAuth")]
public IActionResult RateLimitedAction() => Ok();
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.Core](../Linbik.Core/README.md)
- [Linbik.PasetoAuthManager](../Linbik.PasetoAuthManager/README.md)
- [Linbik.Server](../Linbik.Server/README.md)
- [Linbik.YARP](../Linbik.YARP/README.md)
- [Linbik.Slices](../Linbik.Slices/README.md)

## 📄 License

MIT License

**Contact**: info@linbik.com

---

**Version**: 1.2.0  
**Platform**: ASP.NET Core 10.0 (net10.0)  
**Last Updated**: 9 Eylül 2026

## Local refresh tokens

After a successful callback, an upstream Linbik refresh token is used unchanged.
When none is returned, AuthManager creates a cryptographically random local refresh
 token and stores only its SHA-256 hash and the authenticated user's profile.
POST the configured RefreshPath (default `/api/Linbik/refresh`) to renew either kind.
Local refresh does not call Linbik or mint integration tokens. Upstream failures never
fall back to local sessions. Core's `IAuthService` is an upstream API helper, not
this AuthManager refresh endpoint; do not use it to renew local sessions.

Local tokens rotate atomically on every use. Reusing a consumed token revokes the
whole session. Clients must serialize refresh requests (including across browser tabs).
The absolute session expiry is established at login using RefreshTokenExpirationDays
and is not extended by rotation. LogoutPath revokes the local refresh session and
clears cookies; already-issued access tokens remain valid until their own expiry.

The default InMemoryLinbikRefreshTokenStore is process-local. Restarting loses all
local refresh sessions. It cannot support multiple application instances. Expired
entries are evicted by MemoryCache. No database driver or new package is required.
AuthManager.Shared is linked source compiled into each existing AuthManager assembly;
its public types live under the selected AuthManager's Services namespace.

To use application-owned storage, implement ILinbikRefreshTokenStore and register it
AFTER AddLinbikJwtAuth / AddLinbikPasetoAuth (or before, since defaults use TryAdd):

```csharp
// using Linbik.PasetoAuthManager.Services; // or Linbik.JwtAuthManager.Services
builder.Services.AddScoped<ILinbikRefreshTokenStore, MyRefreshTokenStore>();
```

CreateAsync must persist hashes and immutable session data. RotateAsync must perform
expiry/revocation validation and replacement atomically, retain consumed hashes until
session expiry to detect reuse, and revoke all replacements when reuse is detected.
RevokeAsync must invalidate the entire session even when passed an older hash.
Persisting a custom store also requires stable access-token signing keys across restarts.
Keep those keys in application secret configuration, never source control.