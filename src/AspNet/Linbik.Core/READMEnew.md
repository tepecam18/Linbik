# Linbik.Core

A comprehensive .NET Core library providing authentication, authorization, and user management capabilities for multi-tenant applications.

## 🚀 Features

- **JWT Token Validation** - Secure token validation with PKCE support
- **Multi-Tenant Support** - Built-in tenant isolation
- **User & App Management** - Unified interface for both user and application authentication
- **PKCE Authentication** - Proof Key for Code Exchange implementation
- **HTTP Context Integration** - Seamless integration with ASP.NET Core
- **Extensible Architecture** - Easy to extend and customize

## 📦 Installation

```bash
dotnet add package Linbik.Core
```

## 🔧 Configuration

### Basic Setup

```csharp
// Program.cs
using Linbik.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Linbik Core services
builder.Services.AddLinbik(options =>
{
    options.Version = LinbikVersion.Dev2025;
    options.AppIds = new[] { "app1", "app2" };
    options.AllowAllApp = false;
});

var app = builder.Build();

// Use Linbik middleware
app.UseMiddleware<UnauthorizedPayloadMiddleware>();
```

### Configuration File

```json
// appsettings.json
{
  "Linbik": {
    "Version": "dev2025",
    "AppIds": ["app1", "app2", "app3"],
    "AllowAllApp": false
  }
}
```

## 🏗️ Architecture

### Core Components

- **TokenValidator** - JWT token validation service
- **HttpContextCurrentActor** - Current user/application context provider
- **PkceService** - PKCE authentication implementation
- **LinbikBuilder** - Fluent configuration builder

### Interfaces

- **ICurrentActor** - Current authenticated entity interface
- **ITokenValidator** - Token validation contract
- **ILinbikBuilder** - Configuration builder interface

## 🔐 Authentication

### User Types

```csharp
public enum UserType
{
    Unknown = 0,
    User = 1,
    App = 2
}
```

### Current Actor Properties

```csharp
public interface ICurrentActor
{
    bool IsAuthenticated { get; }
    Guid? UserGuid { get; }
    string? Username { get; }
    string? FirstName { get; }
    string? LastName { get; }
    Guid? AppId { get; }
    string TenantId { get; }
    UserType UserType { get; }
}
```

## 📋 Usage Examples

### Controller Integration

```csharp
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly ICurrentActor _currentActor;
    
    public UserController(ICurrentActor currentActor)
    {
        _currentActor = currentActor;
    }
    
    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        if (!_currentActor.IsAuthenticated)
            return Unauthorized();
            
        if (_currentActor.IsUser)
        {
            return Ok(new
            {
                UserGuid = _currentActor.UserGuid,
                Username = _currentActor.Username,
                FullName = $"{_currentActor.FirstName} {_currentActor.LastName}",
                TenantId = _currentActor.TenantId
            });
        }
        
        return BadRequest("Invalid user type");
    }
}
```

### Token Validation

```csharp
public class AuthService
{
    private readonly ITokenValidator _tokenValidator;
    
    public async Task<TokenValidatorResponse> ValidateUserToken(string token, string verifier)
    {
        return await _tokenValidator.ValidateToken(token, verifier, pkceEnabled: true);
    }
}
```

## 🔒 Security

### PKCE Implementation

The library includes a complete PKCE (Proof Key for Code Exchange) implementation:

```csharp
// Generate PKCE challenge
var (verifier, challenge) = PkceService.Generate();

// Save verifier in secure cookie
PkceService.SaveVerifier(response, verifier);

// Verify challenge
bool isValid = PkceService.VerifyChallengeMatches(verifier, expectedChallenge);
```

### JWT Token Security

- RSA key validation
- Token expiration checking
- App ID validation
- PKCE verification

## 🌐 Multi-Tenant Support

### Tenant Isolation

```csharp
// Each tenant has isolated data
if (_currentActor.TenantId == "company_a")
{
    // Company A specific logic
}
else if (_currentActor.TenantId == "company_b")
{
    // Company B specific logic
}
```

## 📚 API Reference

### LinbikServiceCollectionExtensions

```csharp
public static class LinbikServiceCollectionExtensions
{
    public static LinbikBuilder AddLinbik(this IServiceCollection services, Action<LinbikOptions> configureOptions);
    public static LinbikBuilder AddLinbik(this IServiceCollection services, IConfiguration configuration);
}
```

### LinbikOptions

```csharp
public class LinbikOptions
{
    public string Version { get; set; }
    public string[] AppIds { get; set; }
    public bool AllowAllApp { get; set; }
    public string PublicKey { get; }
}
```

## 🧪 Testing

### Unit Testing

```csharp
[Test]
public void CurrentActor_ShouldReturnCorrectUserType()
{
    // Arrange
    var mockHttpContext = new Mock<HttpContext>();
    var mockUser = new Mock<ClaimsPrincipal>();
    
    // Setup claims
    var claims = new List<Claim>
    {
        new Claim("user_type", "User"),
        new Claim("user_guid", "123e4567-e89b-12d3-a456-426614174000")
    };
    
    mockUser.Setup(u => u.Claims).Returns(claims);
    mockUser.Setup(u => u.Identity.IsAuthenticated).Returns(true);
    
    // Act & Assert
    // ... test implementation
}
```

## 🚀 Performance

- **Lazy Loading** - Claims are loaded only when accessed
- **Caching** - HTTP context accessor for performance
- **Async Operations** - Non-blocking token validation

## 🔧 Customization

### Custom Claim Types

```csharp
public class CustomCurrentActor : ICurrentActor
{
    // Implement custom claim mapping
    public string? CustomProperty => _httpContext.User?.FindFirst("custom_claim")?.Value;
}
```

### Custom Token Validation

```csharp
public class CustomTokenValidator : ITokenValidator
{
    public async Task<TokenValidatorResponse> ValidateToken(string token, string verifier, bool pkceEnabled)
    {
        // Custom validation logic
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

**Linbik.Core** - Building secure, scalable authentication solutions for .NET applications.
