# Linbik.PasetoAuthManager

PASETO Authentication Manager for Linbik Framework. Provides cookie-based login/logout endpoints, PASETO v4 token issuance/validation, and rate limiting — a drop-in alternative to `Linbik.JwtAuthManager` with the same endpoint shape.

## 📦 Installation

```bash
dotnet add package Linbik.PasetoAuthManager
```

## 🚀 Features

- **Cookie-Based PASETO Auth** — Login, callback, logout, and token refresh via minimal API endpoints
- **PASETO v4** — supports both `v4.public` (Ed25519 asymmetric signing) and `v4.local` (XChaCha20-Poly1305 symmetric encryption); see [Choosing a Mode](#-choosing-a-mode) below
- **PKCE Support** — Proof Key for Code Exchange for public clients
- **Rate Limiting** — Shared `LinbikAuth`, `LinbikGeneral`, and `LinbikStrict` policies from `Linbik.Core.Extensions`.
- **Multi-Client Support** — Web, Mobile, Admin via `Clients` configuration
- **Keyless Mode** — Zero-config development with auto-provisioning
- **LinbikAuthorize Attribute** — Protect endpoints with `[LinbikAuthorize]`
- **Fully implemented error paths** — unlike `Linbik.JwtAuthManager`'s `ReturnAuthError` (see `PROJECT_STATUS.md` Known Issues), this package's error handling for login/callback/refresh is complete

## 🔧 Configuration

### Fluent Builder (Recommended)

```csharp
// In Program.cs
using Linbik.Core.Extensions;
using Linbik.PasetoAuthManager.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Linbik services
builder.Services.AddLinbik()
    .AddLinbikPasetoAuth();

var app = builder.Build();

// 2. Validate configuration at startup
app.EnsureLinbik();

// 3. if using controllers, add this.
app.UseRouting();

// 4. Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// 5. Map endpoints: /api/Linbik/login, /api/Linbik/callback, /api/Linbik/logout, /api/Linbik/refresh
app.UseLinbikPasetoAuth();

app.Run();
```

## 🔐 Configure PASETO Authentication

Choose **one** of the following approaches:

```csharp
// Default configuration
AddLinbikPasetoAuth();

// From configuration
AddLinbikPasetoAuth(builder.Configuration.GetSection("Linbik:PasetoAuth"));

// Fluent configuration
AddLinbikPasetoAuth(opt => { });
```

### appsettings.json

```json
{
  "Linbik": {
    "PasetoAuth": {
      "Mode": "Public",
      "PrivateKeyBase64": "base64-ed25519-seed-plus-public-64-bytes",
      "PublicKeyBase64": "base64-ed25519-public-32-bytes",
      "Issuer": "Linbik",
      "Audience": "linbik-client",
      "AccessTokenExpirationMinutes": 60,
      "RefreshTokenExpirationDays": 14,
      "PkceEnabled": true
    }
  }
}
```

### Endpoints

| Path | Method | Description |
|------|--------|-------------|
| `/api/Linbik/login` | GET | Initiate OAuth flow → exchange code → set cookies |
| `/api/Linbik/callback` | GET | OAuth callback for code exchange |
| `/api/Linbik/logout` | GET | Clear all auth cookies |
| `/api/Linbik/refresh` | POST | Refresh tokens using refresh cookie |

## 🔑 Choosing a Mode

`PasetoAuthOptions.Mode` defaults to `PasetoMode.Local` (`v4.local`, symmetric `SharedKeyBase64`) for backwards compatibility, even though this package's PASETO usage is generally described as `v4.public`. Set `Mode = PasetoMode.Public` explicitly and configure `PrivateKeyBase64`/`PublicKeyBase64` (Ed25519) if you want asymmetric signing — recommended for most deployments, since the private key never needs to be shared with token verifiers. Generate keys with `IPasetoHelper.GenerateKeyPair()` (public mode) or `IPasetoHelper.GenerateSymmetricKey()` (local mode).

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
| `LinbikAuth` | Login and logout endpoints (fixed/sliding window) |
| `LinbikStrict` | Callback and refresh endpoints — token exchange (token-bucket) |
| `LinbikGeneral` | Registered for general-purpose use in your own endpoints (more permissive fixed window) |

```csharp
[EnableRateLimiting("LinbikAuth")]
public IActionResult RateLimitedAction() => Ok();
```

> Import `Linbik.Core.Extensions` to use the shared rate limiting extensions with either authentication provider.

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.Core](../Linbik.Core/README.md)
- [Linbik.JwtAuthManager](../Linbik.JwtAuthManager/README.md)
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