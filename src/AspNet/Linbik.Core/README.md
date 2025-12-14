# Linbik.Core

Core library for the Linbik Authentication Framework with Authorization Code Flow support.

## 🚀 Features

### Authorization Code Support (v2.0+)
- **Authorization Code Flow** with PKCE support
- **Multi-Service Integration** - Issue multiple JWT tokens in single response
- **Refresh Token Management** - Long-lived token renewal
- **Per-Service RSA Keys** - Each service gets its own key pair
- **Service Repository** - Manage service registration and validation
- **Rate Limiting** - Built-in protection against abuse
- **HTTP Resilience** - Polly-based retry policies

### Configuration Validation
- Startup-time validation with `IValidateOptions<T>`
- Detailed error messages for missing or invalid configuration

### Exception Handling
- `LinbikException` - Base exception class
- `LinbikAuthenticationException` - Authentication failures
- `LinbikConfigurationException` - Configuration errors
- `LinbikTokenException` - Token operation failures

## 📦 Installation

```bash
dotnet add package Linbik.Core
```

## 🔧 Configuration

### Authorization Code Setup (Recommended)

```csharp
// In Program.cs
builder.Services.AddLinbik(builder.Configuration);
```

```json
// In appsettings.json
{
  "Linbik": {
    "LinbikUrl": "https://linbik.com",
    "ServiceId": "your-service-guid",
    "ClientId": "your-client-guid",
    "ApiKey": "your-api-key",
    "AuthorizationEndpoint": "/auth",
    "TokenEndpoint": "/oauth/token",
    "RefreshEndpoint": "/oauth/refresh",
    "AccessTokenLifetimeMinutes": 60,
    "RefreshTokenLifetimeDays": 14
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
Main authentication service interface with full CancellationToken support.

```csharp
public interface IAuthService
{
    Task RedirectToLinbikAsync(HttpContext context, string? returnUrl = null, 
        string? codeChallenge = null, CancellationToken cancellationToken = default);
    
    Task<LinbikTokenResponse?> ExchangeCodeForTokensAsync(string code, 
        CancellationToken cancellationToken = default);
    
    Task<UserProfile?> GetUserProfileAsync(HttpContext context, 
        CancellationToken cancellationToken = default);
    
    Task<List<LinbikIntegrationToken>> GetIntegrationTokensAsync(HttpContext context, 
        CancellationToken cancellationToken = default);
    
    Task<bool> RefreshTokensAsync(HttpContext context, 
        CancellationToken cancellationToken = default);
    
    Task LogoutAsync(HttpContext context, 
        CancellationToken cancellationToken = default);
}
```

### ILinbikAuthClient
HTTP client for communicating with Linbik OAuth endpoints.

```csharp
public interface ILinbikAuthClient
{
    Task<LinbikTokenResponse?> ExchangeCodeAsync(string code, 
        CancellationToken cancellationToken = default);
    
    Task<LinbikTokenResponse?> RefreshTokensAsync(string refreshToken, 
        CancellationToken cancellationToken = default);
}
```

## 🛡️ Exception Handling

The library provides typed exceptions for better error handling:

```csharp
try
{
    var tokens = await authService.ExchangeCodeForTokensAsync(code);
}
catch (LinbikAuthenticationException ex) when (ex.ErrorCode == LinbikAuthenticationException.InvalidCodeError)
{
    // Handle invalid authorization code
    return RedirectToAction("Login");
}
catch (LinbikTokenException ex) when (ex.ErrorCode == LinbikTokenException.TokenExpiredError)
{
    // Handle expired token
    await authService.RefreshTokensAsync(context);
}
catch (LinbikConfigurationException ex)
{
    // Configuration error - check appsettings.json
    logger.LogError(ex, "Configuration error: {Key}", ex.ConfigurationKey);
}
```

## 📋 Models

### LinbikTokenResponse
Response format for token exchange endpoint.

```csharp
public class LinbikTokenResponse
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string NickName { get; set; }
    public List<LinbikIntegrationToken> Integrations { get; set; }
    public string RefreshToken { get; set; }
    public long? RefreshTokenExpiresAt { get; set; }
    public string? CodeChallenge { get; set; }  // For PKCE validation
}
```

### LinbikIntegrationToken
Per-service JWT token data.

```csharp
public class LinbikIntegrationToken
{
    public string PackageName { get; set; }
    public string ServiceName { get; set; }
    public string Token { get; set; }           // JWT signed with service's private key
    public string ServiceUrl { get; set; }
}
```

### UserProfile
User profile information extracted from JWT.

```csharp
public class UserProfile
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string NickName { get; set; }
}
```

## 🔒 Rate Limiting

Built-in rate limiting to protect authentication endpoints:

```csharp
// In Program.cs
builder.Services.AddLinbikRateLimiting(builder.Configuration);

// In middleware pipeline
app.UseLinbikRateLimiting();
```

```json
// In appsettings.json
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

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.JwtAuthManager](../Linbik.JwtAuthManager/README.md)
- [Linbik.Server](../Linbik.Server/README.md)
- [Linbik.YARP](../Linbik.YARP/README.md)

## 📄 License

This library is currently a work in progress and is not ready for production use.

**Contact**: info@linbik.com

---

**Version**: 2.2.0  
**Last Updated**: 5 Aralık 2025
