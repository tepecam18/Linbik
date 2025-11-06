# Linbik.Core

Core library for the Linbik Authentication Framework with OAuth 2.0 Authorization Code Flow support.

## 🚀 Features

### OAuth 2.0 Support (v2.0+)
- **Authorization Code Flow** with PKCE support
- **Multi-Service Integration** - Issue multiple JWT tokens in single response
- **Refresh Token Management** - Long-lived token renewal
- **Per-Service RSA Keys** - Each service gets its own key pair
- **Service Repository** - Manage service registration and validation

### Legacy Features (Deprecated)
- Simple JWT authentication (use OAuth 2.0 instead)
- Basic token validation (use per-service validation)

## 📦 Installation

```bash
dotnet add package Linbik.Core
```

## 🔧 Configuration

### OAuth 2.0 Setup (Recommended)

```csharp
services.AddLinbik(options =>
{
    options.ServerUrl = "https://linbik.com";
    options.AuthorizationEndpoint = "/auth";
    options.TokenEndpoint = "/oauth/token";
    options.RefreshEndpoint = "/oauth/refresh";
    options.AuthorizationCodeLifetimeMinutes = 10;
    options.AccessTokenLifetimeMinutes = 60;
    options.RefreshTokenLifetimeDays = 30;
    options.EnablePKCE = true;
    options.JwtIssuer = "linbik";
});
```

## 📚 Main Interfaces

### IJwtHelper
RSA-256 JWT token signing and validation for multi-service authentication.

```csharp
public interface IJwtHelper
{
    Task<string> CreateTokenAsync(Claim[] claims, string privateKey, string audience, int expirationMinutes = 60);
    Task<bool> ValidateTokenAsync(string token, string publicKey, string expectedAudience, string expectedIssuer = "linbik");
    Dictionary<string, string> GetTokenClaims(string token);
}
```

### IAuthorizationCodeService
Manages OAuth 2.0 authorization codes (5-10 minute validity, single-use).

```csharp
public interface IAuthorizationCodeService
{
    Task<string> GenerateCodeAsync(/* parameters */);
    Task<(bool isValid, AuthorizationCodeData? data)> ValidateAndUseCodeAsync(string code, Guid serviceId);
    Task<bool> IsCodeValidAsync(string code);
}
```

### IServiceRepository
Service registration and management.

```csharp
public interface IServiceRepository
{
    Task<ServiceData?> GetServiceByIdAsync(Guid serviceId);
    Task<ServiceData?> GetServiceByApiKeyAsync(string apiKey);
    Task<List<ServiceData>> GetGrantedIntegrationServicesAsync(Guid userId, Guid mainServiceId);
    Task<bool> IsIpAllowedAsync(Guid serviceId, string ipAddress);
}
```

### IRefreshTokenService
Refresh token lifecycle management (30-day validity by default).

```csharp
public interface IRefreshTokenService
{
    Task<string> CreateRefreshTokenAsync(/* parameters */);
    Task<(bool isValid, RefreshTokenData? data)> ValidateRefreshTokenAsync(string token, Guid serviceId);
    Task<bool> RevokeRefreshTokenAsync(string token);
}
```

## 📋 Models

### MultiServiceTokenResponse
Response format for token exchange endpoint.

```csharp
public class MultiServiceTokenResponse
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string NickName { get; set; }
    public List<IntegrationToken> Integrations { get; set; }
    public string RefreshToken { get; set; }
    public long RefreshTokenExpiresAt { get; set; }
    public string? CodeChallenge { get; set; }  // For PKCE validation
}
```

### IntegrationToken
Per-service JWT token data.

```csharp
public class IntegrationToken
{
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; }
    public string ServicePackage { get; set; }
    public string BaseUrl { get; set; }
    public string Token { get; set; }           // JWT signed with service's private key
    public int ExpiresIn { get; set; }          // Seconds
    public DateTime ExpiresAt { get; set; }
}
```

## 🔄 Migration from v1.x

Legacy properties are marked with `[Obsolete]` but still functional:

```csharp
// ❌ Old way (v1.x)
options.PublicKey = "single-key-for-all";
options.AllowAllApp = true;

// ✅ New way (v2.0+)
// Use IServiceRepository for service registration
// Use per-service RSA key pairs
// Use proper API key validation
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Migration Guide](../../../MIGRATION_GUIDE.md)
- [Examples](../../../examples/AspNet/AspNet)

## 📄 License

This library is currently a work in progress and is not ready for production use.

**Contact**: info@linbik.com

---

**Version**: 2.0.0 (OAuth 2.0 Support)  
**Last Updated**: 1 Kasım 2025
