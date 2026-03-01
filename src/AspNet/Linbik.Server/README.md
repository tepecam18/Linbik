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

// 1. Add services with public key configuration
builder.Services.AddLinbikServer(options =>
{
    options.ServiceId = "your-service-guid";
    options.PublicKey = builder.Configuration["Linbik:PublicKey"];
    options.ClockSkewMinutes = 5;
    options.ValidateAudience = true;
    options.ValidateIssuer = true;
});

// 2. Use standard ASP.NET Core middleware
app.UseAuthentication();
app.UseAuthorization();

// 3. Protect endpoints with attributes
[ApiController]
public class PaymentController : ControllerBase
{
    // User-context endpoint (kullanıcı bağlamlı)
    [LinbikUserServiceAuthorize]
    [HttpPost("/charge")]
    public IActionResult Charge([FromBody] ChargeRequest request)
    {
        var claims = HttpContext.GetLinbikClaims();
        var userId = HttpContext.GetLinbikUserId();
        return Ok();
    }

    // S2S endpoint — any S2S token accepted
    [LinbikS2SAuthorize]
    [HttpPost("/s2s/sync")]
    public IActionResult SyncData() => Ok();

    // S2S endpoint — only service-to-service tokens (role=Service)
    [LinbikS2SAuthorize("Service")]
    [HttpPost("/s2s/webhook/{eventType}")]
    public IActionResult S2SWebhook(string eventType) => Ok();

    // S2S endpoint — only platform tokens (role=Linbik)
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

## 📚 Main Interface

### ILinbikServerRepository

Complete repository interface for authorization operations:

```csharp
public interface ILinbikServerRepository
{
    // Authorization Code Management
    Task<string> CreateAuthorizationCodeAsync(/* parameters */);
    Task<(bool isValid, AuthorizationCodeData? data)> ValidateAndUseAuthorizationCodeAsync(string code, Guid serviceId);
    
    // Refresh Token Management
    Task<string> CreateRefreshTokenAsync(/* parameters */);
    Task<(bool isValid, RefreshTokenData? data)> ValidateRefreshTokenAsync(string token, Guid serviceId);
    Task<bool> RevokeRefreshTokenAsync(string token);
    
    // User & Profile Management
    Task<UserData?> GetUserByIdAsync(Guid userId);
    Task<ProfileData?> GetUserProfileAsync(Guid userId, Guid profileId);
    
    // Service Management
    Task<ServiceData?> GetServiceByIdAsync(Guid serviceId);
    Task<ServiceData?> GetServiceByApiKeyAsync(string apiKey);
    Task<List<ServiceData>> GetGrantedIntegrationServicesAsync(Guid userId, Guid mainServiceId);
    
    // User Consent Management
    Task SaveUserConsentsAsync(Guid userId, Guid mainServiceId, Guid profileId, List<Guid> grantedServiceIds);
    
    // Legacy Methods (Deprecated)
    [Obsolete] Task<bool> ValidateUserCredentialsAsync(string email, string password);
}
```

## 🔐 JWT Validation (Integration Services)

### IntegrationTokenValidator

Validates integration JWT tokens using RSA public key:

```csharp
public class IntegrationTokenValidator
{
    public IntegrationTokenValidator(IOptions<ServerOptions> options);
    
    // Validate JWT and extract claims
    public LinbikTokenClaims? ValidateToken(string token);
}
```

### LinbikTokenClaims Model

```csharp
public class LinbikTokenClaims
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string NickName { get; set; }
    public string Subject { get; set; }
    public string Audience { get; set; }
    public string Issuer { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
```

### HttpContext Extensions

Easy access to validated Linbik claims:

```csharp
// Get all claims
var claims = HttpContext.GetLinbikClaims();

// Get individual values
var userId = HttpContext.GetLinbikUserId();
var userName = HttpContext.GetLinbikUserName();
var nickName = HttpContext.GetLinbikNickName();

// Check if user is authenticated via Linbik
var isAuthenticated = HttpContext.HasLinbikUser();
```

### Complete Integration Example

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Linbik server with public key (fluent chain)
builder.Services.AddLinbik(builder.Configuration)
    .AddLinbikServer(options =>
    {
        options.PackageName = "payment-gateway";
        options.PublicKey = builder.Configuration["Linbik:PublicKey"];
    });

var app = builder.Build();

// Validate all registered Linbik modules at startup
app.EnsureLinbik();

app.MapPost("/charge", (HttpContext ctx, ChargeRequest request) =>
{
    if (!ctx.HasLinbikUser())
        return Results.Unauthorized();
    
    var userId = ctx.GetLinbikUserId();
    // Process payment...
    
    return Results.Ok(new { success = true });
});

app.Run();
```

## 💻 Usage Examples

### 1. Authorization Code Flow

#### Generate Authorization Code

```csharp
public class AuthController : Controller
{
    private readonly ILinbikServerRepository _repository;

    [HttpGet("/auth/{serviceId}/{codeChallenge?}")]
    public async Task<IActionResult> Authorize(Guid serviceId, string? codeChallenge)
    {
        // 1. Check user authentication (cookie)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToAction("Login");
        
        // 2. Get service details
        var service = await _repository.GetServiceByIdAsync(serviceId);
        if (service == null) return NotFound("Service not found");
        
        // 3. Get integration services (if any)
        var integrations = await _repository.GetGrantedIntegrationServicesAsync(
            Guid.Parse(userId), 
            serviceId
        );
        
        // 4. Show consent screen or auto-generate code
        // (Implementation depends on your UI)
        
        // 5. Generate authorization code
        var code = await _repository.CreateAuthorizationCodeAsync(
            serviceId: serviceId,
            userId: Guid.Parse(userId),
            profileId: userProfileId,
            grantedIntegrationServiceIds: integrations.Select(i => i.Id).ToArray(),
            codeChallenge: codeChallenge,
            expiresAt: DateTime.UtcNow.AddMinutes(10)
        );
        
        // 6. Redirect to service callback
        return Redirect($"{service.BaseUrl}{service.CallbackPath}?code={code}");
    }
}
```

#### Token Exchange

```csharp
[HttpPost("/oauth/token")]
public async Task<IActionResult> ExchangeToken()
{
    // 1. Extract headers
    var apiKey = Request.Headers["ApiKey"].ToString();
    var code = Request.Headers["Code"].ToString();
    
    // 2. Validate service by API key
    var service = await _repository.GetServiceByApiKeyAsync(apiKey);
    if (service == null) return Unauthorized("Invalid API key");
    
    // 3. Validate and consume authorization code
    var (isValid, authData) = await _repository.ValidateAndUseAuthorizationCodeAsync(
        code, 
        service.Id
    );
    if (!isValid) return BadRequest("Invalid or expired code");
    
    // 4. Get user profile
    var profile = await _repository.GetUserProfileAsync(
        authData.UserId, 
        authData.ProfileId
    );
    
    // 5. Get integration services
    var integrationServices = await _repository.GetGrantedIntegrationServicesAsync(
        authData.UserId, 
        service.Id
    );
    
    // 6. Generate JWT for each integration service
    var integrationTokens = new List<IntegrationToken>();
    foreach (var integration in integrationServices)
    {
        var token = await _jwtHelper.CreateTokenAsync(
            claims: new[] {
                new Claim("userId", authData.UserId.ToString()),
                new Claim("userName", profile.UserName),
                new Claim("nickName", profile.NickName)
            },
            privateKey: integration.PrivateKey,
            audience: integration.Id.ToString(),
            expirationMinutes: 60
        );
        
        integrationTokens.Add(new IntegrationToken
        {
            ServiceId = integration.Id,
            ServiceName = integration.Name,
            ServicePackage = integration.PackageName,
            BaseUrl = integration.BaseUrl,
            Token = token,
            ExpiresIn = 3600,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
    }
    
    // 7. Create refresh token
    var refreshToken = await _repository.CreateRefreshTokenAsync(
        serviceId: service.Id,
        userId: authData.UserId,
        profileId: authData.ProfileId,
        grantedIntegrationServiceIds: integrationServices.Select(i => i.Id).ToArray(),
        expiresAt: DateTime.UtcNow.AddDays(30)
    );
    
    // 8. Return response
    return Ok(new MultiServiceTokenResponse
    {
        UserId = authData.UserId,
        UserName = profile.UserName,
        NickName = profile.NickName,
        Integrations = integrationTokens,
        RefreshToken = refreshToken,
        RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
        CodeChallenge = authData.CodeChallenge  // For PKCE validation
    });
}
```

### 2. Refresh Token Flow

```csharp
[HttpPost("/oauth/refresh")]
public async Task<IActionResult> RefreshToken()
{
    var apiKey = Request.Headers["ApiKey"].ToString();
    var refreshToken = Request.Headers["RefreshToken"].ToString();
    
    // 1. Validate service
    var service = await _repository.GetServiceByApiKeyAsync(apiKey);
    if (service == null) return Unauthorized();
    
    // 2. Validate refresh token
    var (isValid, tokenData) = await _repository.ValidateRefreshTokenAsync(
        refreshToken, 
        service.Id
    );
    if (!isValid) return BadRequest("Invalid or expired refresh token");
    
    // 3. Get user profile
    var profile = await _repository.GetUserProfileAsync(
        tokenData.UserId, 
        tokenData.ProfileId
    );
    
    // 4. Re-generate integration tokens (same logic as token exchange)
    // ...
    
    return Ok(new MultiServiceTokenResponse { /* ... */ });
}
```

### 3. User Consent Management

```csharp
[HttpPost("/auth/consent")]
public async Task<IActionResult> SaveConsent(ConsentModel model)
{
    await _repository.SaveUserConsentsAsync(
        userId: model.UserId,
        mainServiceId: model.ServiceId,
        profileId: model.ProfileId,
        grantedServiceIds: model.SelectedIntegrationIds
    );
    
    // Continue with authorization code generation...
    return RedirectToAction("Authorize");
}
```

## 🗄️ Data Models

### AuthorizationCodeData

```csharp
public class AuthorizationCodeData
{
    public string Code { get; set; }
    public Guid ServiceId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid[] GrantedIntegrationServiceIds { get; set; }
    public string? CodeChallenge { get; set; }  // For PKCE
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
}
```

### RefreshTokenData

```csharp
public class RefreshTokenData
{
    public string Token { get; set; }
    public Guid ServiceId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid[] GrantedIntegrationServiceIds { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}
```

### ServiceData

```csharp
public class ServiceData
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string PackageName { get; set; }
    public string BaseUrl { get; set; }
    public string CallbackPath { get; set; }
    public string ApiKey { get; set; }
    public string? PrivateKey { get; set; }      // For integration services
    public string? PublicKey { get; set; }       // For integration services
    public bool IsIntegrationService { get; set; }
    public string? AllowedIPs { get; set; }      // CIDR notation
}
```

## 🔒 Security Considerations

### API Key Validation
✅ Always validate API key before token operations  
✅ Use secure random generation for API keys  
✅ Consider hashing API keys in database (TODO in Linbik.App)

### Authorization Code Security
✅ Single-use codes (mark as used immediately)  
✅ Short expiration (5-10 minutes recommended)  
✅ Bind to specific service ID  
✅ Optional PKCE support for public clients

### Refresh Token Security
✅ Long expiration (30 days default)  
✅ Store securely with revocation capability  
✅ Invalidate on logout or suspicious activity  
✅ Rotate on each refresh (optional)

### IP Whitelisting
```csharp
// Check if request IP is allowed
if (!string.IsNullOrEmpty(service.AllowedIPs))
{
    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
    if (!IsIpAllowed(clientIp, service.AllowedIPs))
        return Forbid("IP not allowed");
}
```

## 🔄 Migration from v1.x

```csharp
// ❌ Old way (v1.x)
var isValid = await _repository.ValidateUserCredentialsAsync(email, password);

// ✅ New way (v2.0+)
// Use authorization code flow with cookies
// Client redirects to /auth/{serviceId}
// Server generates authorization code
// Client exchanges code for tokens
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
