# Linbik.JwtAuthManager

A comprehensive JWT authentication manager for .NET applications, providing secure token management, PKCE support, and flexible authentication schemes.

## 🚀 Features

- **JWT Token Management** - Complete JWT lifecycle management
- **PKCE Authentication** - Proof Key for Code Exchange implementation
- **Refresh Token Support** - Secure token refresh mechanism
- **Multi-App Support** - Manage multiple application authentications
- **Cookie-Based Tokens** - Secure HTTP-only cookie storage
- **Flexible Configuration** - Easy-to-configure authentication options
- **In-Memory Repository** - Built-in token storage (can be extended)

## 📦 Installation

```bash
dotnet add package Linbik.JwtAuthManager
```

## 🔧 Configuration

### Basic Setup

```csharp
// Program.cs
using Linbik.Core.Extensions;
using Linbik.JwtAuthManager;

var builder = WebApplication.CreateBuilder(args);

// Add Linbik Core first
var linbikBuilder = builder.Services.AddLinbik(options =>
{
    options.Version = LinbikVersion.Dev2025;
    options.AppIds = new[] { "app1", "app2" };
    options.AllowAllApp = false;
});

// Add JWT Auth Manager
linbikBuilder.AddJwtAuth(jwtOptions =>
{
    jwtOptions.PrivateKey = "your-secret-key-here";
    jwtOptions.Algorithm = SecurityAlgorithms.HmacSha512Signature;
    jwtOptions.PkceEnabled = true;
    jwtOptions.AccessTokenExpiration = 15; // minutes
    jwtOptions.RefreshTokenExpiration = 15; // days
    jwtOptions.LoginPath = "/linbik/login";
    jwtOptions.RefreshLoginPath = "/linbik/refresh-token";
    jwtOptions.ExitPath = "/linbik/logout";
    jwtOptions.PkceStartPath = "/linbik/pkce-start";
}, useInMemory: true);

var app = builder.Build();

// Use JWT Auth endpoints
app.UseJwtAuth();
```

### Configuration File

```json
// appsettings.json
{
  "Linbik": {
    "JwtAuth": {
      "PrivateKey": "your-secret-key-here",
      "Algorithm": "HS512",
      "PkceEnabled": true,
      "AccessTokenExpiration": 15,
      "RefreshTokenExpiration": 15,
      "LoginPath": "/linbik/login",
      "RefreshLoginPath": "/linbik/refresh-token",
      "ExitPath": "/linbik/logout",
      "PkceStartPath": "/linbik/pkce-start",
      "RefererControl": false
    }
  }
}
```

## 🏗️ Architecture

### Core Components

- **JwtAuthService** - Main authentication service
- **InMemoryLinbikRepository** - Token storage implementation
- **AuthenticationBuilderExtensions** - JWT Bearer configuration
- **JwtAuthManagerExtensions** - Endpoint and service configuration

### Interfaces

- **ILinbikRepository** - Token storage contract
- **IAuthService** - Authentication service contract

## 🔐 Authentication Flow

### 1. PKCE Start

```http
POST /linbik/pkce-start
```

**Response:**
```json
{
  "isSuccess": true,
  "data": {
    "code_challenge": "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"
  }
}
```

### 2. User Login

```http
GET /linbik/login?token={jwt_token}&verifier={pkce_verifier}&route={redirect_route}&returnUrl={return_url}
```

**Process:**
1. Validate JWT token
2. Verify PKCE challenge
3. Generate refresh token
4. Set secure cookies
5. Redirect to specified route

### 3. Token Refresh

```http
POST /linbik/refresh-token
```

**Process:**
1. Extract refresh token from cookie
2. Validate refresh token
3. Generate new access token
4. Update cookies

### 4. Logout

```http
POST /linbik/logout
```

**Process:**
1. Clear all authentication cookies
2. Invalidate refresh token

## 📋 Usage Examples

### Controller Integration

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var userId = await _authService.GetUserIdAsync(HttpContext);
        // Process login logic
        return Ok(new { UserId = userId });
    }
}
```

### Custom Repository Implementation

```csharp
public class SqlServerLinbikRepository : ILinbikRepository
{
    private readonly DbContext _context;
    
    public async Task<(string refreshToken, bool success)> CreateRefresToken(Guid userGuid, string name)
    {
        var refreshToken = GenerateSecureToken();
        
        var tokenModel = new TokenModel
        {
            RefreshToken = refreshToken,
            Expiration = DateTime.UtcNow.AddDays(15),
            UserGuid = userGuid,
            Name = name
        };
        
        _context.Tokens.Add(tokenModel);
        await _context.SaveChangesAsync();
        
        return (refreshToken, true);
    }
    
    public async Task<TokenValidatorResponse> UseRefresToken(string token)
    {
        var tokenData = await _context.Tokens
            .FirstOrDefaultAsync(t => t.RefreshToken == token);
            
        if (tokenData == null || tokenData.Expiration < DateTime.UtcNow)
        {
            return new TokenValidatorResponse
            {
                Success = false,
                Message = "Invalid or expired refresh token"
            };
        }
        
        return new TokenValidatorResponse
        {
            Success = true,
            UserGuid = tokenData.UserGuid,
            Name = tokenData.Name
        };
    }
}
```

## 🔒 Security Features

### PKCE Implementation

```csharp
// Generate PKCE challenge and verifier
var (verifier, challenge) = PkceService.Generate();

// Save verifier in secure cookie
PkceService.SaveVerifier(response, verifier);

// Verify challenge during login
bool isValid = PkceService.VerifyChallengeMatches(verifier, expectedChallenge);
```

### Cookie Security

```csharp
var cookieOptions = new CookieOptions
{
    HttpOnly = true,           // Prevent XSS attacks
    Secure = true,             // HTTPS only
    SameSite = SameSiteMode.None, // Cross-site requests
    Expires = DateTime.UtcNow.AddDays(15)
};
```

### JWT Token Security

- HMAC-SHA512 signature
- Configurable expiration times
- PKCE verification
- App ID validation

## 🌐 Multi-App Support

### App Configuration

```csharp
var jwtOptions = new JwtAuthOptions
{
    PrivateKey = "your-secret-key",
    PkceEnabled = true,
    AccessTokenExpiration = 15,
    RefreshTokenExpiration = 15,
    Routes = new Dictionary<string, string>
    {
        ["app1"] = "https://app1.company.com",
        ["app2"] = "https://app2.company.com",
        ["default"] = "https://dashboard.company.com"
    }
};
```

### Route-Based Redirects

```csharp
// During login, redirect based on route parameter
var routeKey = context.Request.Query["route"].FirstOrDefault() ?? "default";
options.Routes.TryGetValue(routeKey, out var redirectUrl);

if (!string.IsNullOrEmpty(redirectUrl))
{
    return Results.Redirect(redirectUrl);
}
```

## 📚 API Reference

### JwtAuthOptions

```csharp
public class JwtAuthOptions
{
    public string PrivateKey { get; set; }
    public string Algorithm { get; set; }
    public string LoginPath { get; set; }
    public string RefreshLoginPath { get; set; }
    public string ExitPath { get; set; }
    public string PkceStartPath { get; set; }
    public bool PkceEnabled { get; set; }
    public int AccessTokenExpiration { get; set; }
    public int RefreshTokenExpiration { get; set; }
    public Dictionary<string, string> Routes { get; set; }
    public bool RefererControl { get; set; }
}
```

### ILinbikRepository

```csharp
public interface ILinbikRepository
{
    Task<(string refreshToken, bool success)> CreateRefresToken(Guid userGuid, string name);
    Task<TokenValidatorResponse> UseRefresToken(string token);
    Task LoggedInUser(Guid userGuid, string name);
}
```

## 🧪 Testing

### Unit Testing

```csharp
[Test]
public async Task CreateRefresToken_ShouldReturnValidToken()
{
    // Arrange
    var repository = new InMemoryLinbikRepository(Mock.Of<IOptions<JwtAuthOptions>>());
    var userGuid = Guid.NewGuid();
    var userName = "testuser";
    
    // Act
    var (refreshToken, success) = await repository.CreateRefresToken(userGuid, userName);
    
    // Assert
    Assert.IsTrue(success);
    Assert.IsNotNull(refreshToken);
    Assert.IsTrue(refreshToken.Length > 0);
}
```

### Integration Testing

```csharp
[Test]
public async Task LoginEndpoint_ShouldReturnValidResponse()
{
    // Arrange
    var client = _factory.CreateClient();
    var token = GenerateValidJwtToken();
    var verifier = "test_verifier";
    
    // Act
    var response = await client.GetAsync($"/linbik/login?token={token}&verifier={verifier}");
    
    // Assert
    Assert.IsTrue(response.IsSuccessStatusCode);
}
```

## 🚀 Performance

- **In-Memory Storage** - Fast token access
- **Async Operations** - Non-blocking token operations
- **Efficient Validation** - Optimized JWT validation
- **Cookie Management** - Secure and performant cookie handling

## 🔧 Customization

### Custom Token Storage

```csharp
// Register custom repository
builder.Services.AddSingleton<ILinbikRepository, CustomTokenRepository>();

// Use custom repository
linbikBuilder.AddJwtAuth(jwtOptions => { }, useInMemory: false);
```

### Custom Authentication Schemes

```csharp
public static class CustomAuthenticationExtensions
{
    public static AuthenticationBuilder AddCustomScheme(this AuthenticationBuilder builder)
    {
        return builder.AddJwtBearer("CustomScheme", options =>
        {
            // Custom JWT configuration
        });
    }
}
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📞 Support

For support and questions:
- Create an issue in the repository
- Contact the development team
- Check the documentation

---

**Linbik.JwtAuthManager** - Secure JWT authentication management for modern .NET applications.
