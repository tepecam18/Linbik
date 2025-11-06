# Linbik.JwtAuthManager

JWT Authentication Manager with RSA-256 signing support for Linbik Framework.

## 🚀 Features

### OAuth 2.0 Support (v2.0+)
- **RSA-256 JWT Signing** - Industry-standard asymmetric cryptography
- **Per-Service Key Pairs** - Each service uses its own private/public keys
- **Multi-Service Token Generation** - Issue multiple JWTs in single response
- **PKCS#8 & X.509 Support** - Standard PEM key formats
- **Token Validation** - Verify JWT signatures with public keys
- **Claims Extraction** - Parse and validate JWT claims

### Legacy Features (Deprecated)
- Symmetric key JWT signing (HS256) - Use RSA-256 instead
- Single-key authentication - Use per-service keys

## 📦 Installation

```bash
dotnet add package Linbik.JwtAuthManager
```

## 🔧 Configuration

### Basic Setup

```csharp
services.AddLinbikJwtAuth(options =>
{
    options.Issuer = "linbik";
    options.DefaultAudience = "my-service";
    options.AccessTokenLifetimeMinutes = 60;
    options.RefreshTokenLifetimeDays = 30;
    options.ClockSkewMinutes = 1;
});
```

### RSA Key Configuration

```csharp
// For each service that needs to issue tokens
options.ServiceKeyPairs.Add(new ServiceKeyPair
{
    ServiceId = Guid.Parse("service-guid"),
    PrivateKeyPem = "-----BEGIN PRIVATE KEY-----\n...",  // PKCS#8
    PublicKeyPem = "-----BEGIN PUBLIC KEY-----\n..."     // X.509 SPKI
});
```

## 💻 Usage

### Create JWT Token

```csharp
public class TokenService
{
    private readonly IJwtHelper _jwtHelper;

    public TokenService(IJwtHelper jwtHelper)
    {
        _jwtHelper = jwtHelper;
    }

    public async Task<string> CreateAccessTokenAsync(User user, Service service)
    {
        var claims = new[]
        {
            new Claim("userId", user.Id.ToString()),
            new Claim("userName", user.UserName),
            new Claim("nickName", user.NickName)
        };

        return await _jwtHelper.CreateTokenAsync(
            claims: claims,
            privateKey: service.PrivateKey,      // PKCS#8 PEM
            audience: service.Id.ToString(),
            expirationMinutes: 60
        );
    }
}
```

### Validate JWT Token

```csharp
public async Task<bool> ValidateTokenAsync(string token, Service service)
{
    return await _jwtHelper.ValidateTokenAsync(
        token: token,
        publicKey: service.PublicKey,            // X.509 SPKI PEM
        expectedAudience: service.Id.ToString(),
        expectedIssuer: "linbik"
    );
}
```

### Extract Claims

```csharp
public Dictionary<string, string> GetUserInfoFromToken(string token)
{
    var claims = _jwtHelper.GetTokenClaims(token);
    
    // claims["userId"], claims["userName"], claims["exp"], etc.
    return claims;
}
```

## 🏗️ Architecture

### JwtHelperService Implementation

```
Token Creation Flow:
1. Parse claims array
2. Import RSA private key (PKCS#8 PEM)
3. Create token descriptor with RS256 algorithm
4. Sign token with RSA key
5. Return base64-encoded JWT string

Token Validation Flow:
1. Import RSA public key (X.509 SPKI PEM)
2. Configure validation parameters (issuer, audience, lifetime)
3. Validate signature with RSA key
4. Verify claims (exp, iss, aud)
5. Return true/false
```

### Key Format Support

**Private Key (PKCS#8 PEM)**:
```
-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC...
-----END PRIVATE KEY-----
```

**Public Key (X.509 SPKI PEM)**:
```
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAr8zN...
-----END PUBLIC KEY-----
```

## 📋 Repository Interface

### ILinbikRepository

```csharp
public interface ILinbikRepository
{
    // OAuth 2.0 Methods (v2.0+)
    Task<ServiceData?> GetServiceByApiKeyAsync(string apiKey);
    Task<ServiceData?> GetServiceByIdAsync(Guid serviceId);
    Task<List<ServiceData>> GetGrantedIntegrationServicesAsync(Guid userId, Guid mainServiceId);
    
    // Legacy Methods (Deprecated)
    [Obsolete("Use GetServiceByIdAsync")]
    Task<AppModel?> GetAppByIdAsync(Guid appId);
}
```

### InMemoryLinbikRepository

In-memory implementation for testing:

```csharp
services.AddSingleton<ILinbikRepository, InMemoryLinbikRepository>();

// Pre-populate with test data
var repo = serviceProvider.GetRequiredService<InMemoryLinbikRepository>();
repo.AddService(new ServiceData
{
    Id = Guid.NewGuid(),
    PackageName = "test-service",
    ApiKey = "linbik_test123",
    PrivateKey = "-----BEGIN PRIVATE KEY-----...",
    PublicKey = "-----BEGIN PUBLIC KEY-----...",
    IsIntegrationService = true
});
```

## 🔒 Security Features

### RSA-256 (RS256)
- ✅ **Asymmetric Cryptography**: Private key signs, public key verifies
- ✅ **2048-bit Keys**: Industry-standard key length
- ✅ **No Shared Secrets**: Public keys can be distributed safely
- ✅ **Per-Service Keys**: Each service has unique key pair

### Token Validation
- ✅ **Signature Verification**: RSA signature check
- ✅ **Issuer Validation**: Verify token issuer claim
- ✅ **Audience Validation**: Verify intended recipient
- ✅ **Expiration Check**: Enforce token lifetime
- ✅ **Clock Skew Tolerance**: 1-minute default tolerance

## 🔄 Migration from v1.x

```csharp
// ❌ Old way (v1.x - Symmetric key)
var token = _jwtHelper.CreateToken(claims, sharedSecret);

// ✅ New way (v2.0+ - Asymmetric RSA)
var token = await _jwtHelper.CreateTokenAsync(
    claims, 
    service.PrivateKey, 
    audience: service.Id.ToString()
);
```

Legacy methods still work but are marked `[Obsolete]`:
- `CreateToken(claims, sharedSecret)` → Use `CreateTokenAsync` with RSA key
- `ValidateToken(token, sharedSecret)` → Use `ValidateTokenAsync` with public key
- `GetAppByIdAsync(appId)` → Use `GetServiceByIdAsync(serviceId)`

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [Migration Guide](../../../MIGRATION_GUIDE.md)
- [Examples](../../../examples/AspNet/AspNet)

## 📄 License

This library is currently a work in progress and is not ready for production use.

**Contact**: info@linbik.com

---

**Version**: 2.0.0 (RSA-256 Support)  
**Last Updated**: 1 Kasım 2025
