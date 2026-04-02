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
- **S2S (Service-to-Service)** token operations
- **Keyless Mode** — Zero-configuration development
- **Heartbeat** — SDK-to-server health signals
- **Configuration Validation** — Startup-time validation with `IValidateOptions<T>`
- **HTTP Resilience** — Polly-based retry policies
- **Health Checks** — Built-in health check integration

## 🔧 Configuration

### Basic Setup (Fluent Builder)

```csharp
// In Program.cs
builder.Services.AddLinbik(builder.Configuration.GetSection("Linbik"))
    .AddLinbikJwtAuth()      // optional: Linbik.JwtAuthManager
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
    "S2STokenEndpoint": "/api/auth/s2s-token",
    "S2STargetServices": {
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

    // S2S (Service-to-Service) Token Operations
    Task<LinbikS2STokenResponse?> GetS2STokensAsync(
        LinbikS2STokenRequest request, CancellationToken cancellationToken = default);

    Task<LinbikS2STokenResponse?> GetS2STokensAsync(
        IEnumerable<string> targetPackageNames, CancellationToken cancellationToken = default);

    // Client Management
    Task<bool> UpdateClientRedirectUriByNameAsync(
        string clientName, string redirectUri, CancellationToken cancellationToken = default);
}
```

### IJwtHelper

JWT token generation and validation helper.

```csharp
public interface IJwtHelper
{
    Task<string> CreateTokenAsync(Claim[] claims, string privateKey,
        string audience, int expirationMinutes = 60);

    Task<bool> ValidateTokenAsync(string token, string publicKey,
        string expectedAudience, string expectedIssuer = "Linbik");

    Dictionary<string, string> GetTokenClaims(string token);
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
    public string UserName { get; set; }
    public string NickName { get; set; }
    public Dictionary<string, string> IntegrationTokens { get; set; }
}
```

### S2S Models

```csharp
public sealed class LinbikS2STokenRequest
{
    public Guid SourceServiceId { get; set; }
    public List<Guid> TargetServiceIds { get; set; }
}

public sealed class LinbikS2STokenResponse
{
    public Guid SourceServiceId { get; set; }
    public string SourcePackageName { get; set; }
    public List<LinbikS2SIntegration> Integrations { get; set; }
    public long AccessTokenExpiresAt { get; set; }
}

public sealed class LinbikS2SIntegration
{
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; }
    public string PackageName { get; set; }
    public string ServiceUrl { get; set; }
    public string Token { get; set; }
}
```

## 🛡️ Exception Handling

```csharp
try
{
    var tokens = await authService.ExchangeCodeForTokensAsync(code);
}
catch (LinbikAuthenticationException ex) when (ex.ErrorCode == LinbikAuthenticationException.InvalidCodeError)
{
    return RedirectToAction("Login");
}
catch (LinbikTokenException ex) when (ex.ErrorCode == LinbikTokenException.TokenExpiredError)
{
    await authService.RefreshTokensAsync(context);
}
catch (LinbikConfigurationException ex)
{
    logger.LogError(ex, "Configuration error: {Key}", ex.ConfigurationKey);
}
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.JwtAuthManager](../Linbik.JwtAuthManager/README.md)
- [Linbik.Server](../Linbik.Server/README.md)
- [Linbik.YARP](../Linbik.YARP/README.md)

## 📄 License

MIT License

**Contact**: info@linbik.com

---

**Version**: 1.2.0  
**Platform**: ASP.NET Core 10.0 (net10.0)  
**Last Updated**: 2 Nisan 2026
