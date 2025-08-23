# Linbik.Server

A server-side authentication service for .NET applications, providing secure app-to-app authentication, JWT token generation, and flexible authentication schemes.

## 🚀 Features

- **App-to-App Authentication** - Secure authentication between applications
- **JWT Token Generation** - Custom JWT token creation with configurable claims
- **Flexible Authentication Schemes** - Multiple authentication options
- **Secure Token Storage** - Encrypted token management
- **Multi-Tenant Support** - Built-in tenant isolation
- **Easy Integration** - Simple setup and configuration
- **Extensible Architecture** - Customizable authentication flows

## 📦 Installation

```bash
dotnet add package Linbik.Server
```

## 🔧 Configuration

### Basic Setup

```csharp
// Program.cs
using Linbik.Core.Extensions;
using Linbik.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Linbik Core first
var linbikBuilder = builder.Services.AddLinbik(options =>
{
    options.Version = LinbikVersion.Dev2025;
    options.AppIds = new[] { "app1", "app2" };
    options.AllowAllApp = false;
});

// Add Linbik Server
linbikBuilder.AddLinbikServer(serverOptions =>
{
    serverOptions.PrivateKey = "your-secret-key-here";
    serverOptions.Algorithm = SecurityAlgorithms.HmacSha512Signature;
    serverOptions.LoginPath = "/linbik/app-login";
    serverOptions.AccessTokenExpiration = 60; // minutes
});

var app = builder.Build();

// Use Linbik Server endpoints
app.UseLinbikServer();
```

### Configuration File

```json
// appsettings.json
{
  "Linbik": {
    "Server": {
      "PrivateKey": "your-secret-key-here",
      "Algorithm": "HS512",
      "LoginPath": "/linbik/app-login",
      "AccessTokenExpiration": 60
    }
  }
}
```

## 🏗️ Architecture

### Core Components

- **ServerExtensions** - Main configuration and endpoint setup
- **AuthenticationBuilderExtensions** - JWT Bearer authentication configuration
- **ServerOptions** - Configuration options
- **AppLoginModel** - Login request model

### Interfaces

- **ILinbikServerRepository** - Server-side authentication repository contract

## 🔐 Authentication Flow

### App Login Process

```http
POST /linbik/app-login
Content-Type: application/json

{
  "appId": "123e4567-e89b-12d3-a456-426614174000",
  "key": "your-app-secret-key"
}
```

**Response:**
```json
{
  "isSuccess": true,
  "data": {
    "token": "eyJhbGciOiJIUzUxMiIsInR5cCI6IkpXVCJ9...",
    "expiresIn": 60
  }
}
```

### JWT Token Claims

The generated JWT token includes the following claims:

```json
{
  "app_id": "123e4567-e89b-12d3-a456-426614174000",
  "user_type": "App",
  "tenant_id": "default",
  "name": "App 123e4567-e89b-12d3-a456-426614174000",
  "nameidentifier": "123e4567-e89b-12d3-a456-426614174000",
  "exp": 1640995200
}
```

## 📋 Usage Examples

### Controller Integration

```csharp
[ApiController]
[Route("api/[controller]")]
public class AppController : ControllerBase
{
    private readonly ICurrentActor _currentActor;
    
    public AppController(ICurrentActor currentActor)
    {
        _currentActor = currentActor;
    }
    
    [HttpGet("info")]
    public IActionResult GetAppInfo()
    {
        if (!_currentActor.IsAuthenticated)
            return Unauthorized();
            
        if (_currentActor.IsApp)
        {
            return Ok(new
            {
                AppId = _currentActor.AppId,
                TenantId = _currentActor.TenantId,
                UserType = _currentActor.UserType.ToString()
            });
        }
        
        return BadRequest("Invalid app type");
    }
}
```

### Custom Repository Implementation

```csharp
public class SqlServerLinbikServerRepository : ILinbikServerRepository
{
    private readonly DbContext _context;
    
    public async Task<AppLoginValidationResponse> AppLoginValidationsAsync(AppLoginModel request)
    {
        // Validate app credentials
        var app = await _context.Apps
            .FirstOrDefaultAsync(a => a.AppId == request.AppId && a.Key == request.Key);
            
        if (app == null)
        {
            return new AppLoginValidationResponse
            {
                Success = false,
                Message = "Invalid app credentials"
            };
        }
        
        // Generate claims
        var claims = new List<Claim>
        {
            new Claim("app_id", app.AppId.ToString()),
            new Claim("app_name", app.Name),
            new Claim("tenant_id", app.TenantId)
        };
        
        return new AppLoginValidationResponse
        {
            Success = true,
            Claims = claims
        };
    }
}
```

## 🔒 Security Features

### JWT Token Security

```csharp
var tokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    ValidateLifetime = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.PrivateKey)),
    ValidateIssuer = false,
    ValidateAudience = false,
};
```

### Secure Key Management

- HMAC-SHA512 signature algorithm
- Configurable private key
- Token expiration validation
- Secure claim generation

## 🌐 Multi-Tenant Support

### Tenant Isolation

```csharp
// Each app can belong to different tenants
if (_currentActor.TenantId == "company_a")
{
    // Company A specific logic
    var companyData = await _context.CompanyAData.ToListAsync();
}
else if (_currentActor.TenantId == "company_b")
{
    // Company B specific logic
    var companyData = await _context.CompanyBData.ToListAsync();
}
```

### App Configuration

```csharp
var serverOptions = new ServerOptions
{
    PrivateKey = "your-secret-key",
    Algorithm = SecurityAlgorithms.HmacSha512Signature,
    LoginPath = "/linbik/app-login",
    AccessTokenExpiration = 60
};
```

## 📚 API Reference

### ServerOptions

```csharp
public class ServerOptions
{
    public string PrivateKey { get; set; }
    public string Algorithm { get; set; }
    public string LoginPath { get; set; }
    public int AccessTokenExpiration { get; set; }
}
```

### AppLoginModel

```csharp
public class AppLoginModel
{
    public Guid AppId { get; set; }
    public string Key { get; set; }
}
```

### ILinbikServerRepository

```csharp
public interface ILinbikServerRepository
{
    Task<AppLoginValidationResponse> AppLoginValidationsAsync(AppLoginModel request);
}
```

### AppLoginValidationResponse

```csharp
public class AppLoginValidationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<Claim> Claims { get; set; }
}
```

## 🧪 Testing

### Unit Testing

```csharp
[Test]
public async Task AppLoginValidationsAsync_ShouldReturnValidResponse()
{
    // Arrange
    var repository = new MockLinbikServerRepository();
    var request = new AppLoginModel
    {
        AppId = Guid.NewGuid(),
        Key = "valid-key"
    };
    
    // Act
    var response = await repository.AppLoginValidationsAsync(request);
    
    // Assert
    Assert.IsTrue(response.Success);
    Assert.IsNotNull(response.Claims);
    Assert.IsTrue(response.Claims.Count > 0);
}
```

### Integration Testing

```csharp
[Test]
public async Task AppLoginEndpoint_ShouldReturnValidToken()
{
    // Arrange
    var client = _factory.CreateClient();
    var loginRequest = new AppLoginModel
    {
        AppId = Guid.NewGuid(),
        Key = "valid-key"
    };
    
    // Act
    var response = await client.PostAsJsonAsync("/linbik/app-login", loginRequest);
    
    // Assert
    Assert.IsTrue(response.IsSuccessStatusCode);
    
    var result = await response.Content.ReadFromJsonAsync<LBaseResponse<AppLoginResponse>>();
    Assert.IsTrue(result.IsSuccess);
    Assert.IsNotNull(result.Data.Token);
}
```

## 🚀 Performance

- **Efficient JWT Generation** - Fast token creation
- **Async Operations** - Non-blocking authentication
- **Optimized Validation** - Quick app credential validation
- **Memory Management** - Efficient claim handling

## 🔧 Customization

### Custom Authentication Logic

```csharp
public class CustomServerRepository : ILinbikServerRepository
{
    public async Task<AppLoginValidationResponse> AppLoginValidationsAsync(AppLoginModel request)
    {
        // Custom validation logic
        var isValid = await ValidateAppCredentials(request);
        
        if (!isValid)
        {
            return new AppLoginValidationResponse
            {
                Success = false,
                Message = "Custom validation failed"
            };
        }
        
        // Custom claims generation
        var claims = GenerateCustomClaims(request);
        
        return new AppLoginValidationResponse
        {
            Success = true,
            Claims = claims
        };
    }
}
```

### Custom JWT Configuration

```csharp
public static class CustomServerExtensions
{
    public static IApplicationBuilder UseCustomLinbikServer(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<ServerOptions>>().Value;
        
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/custom/login", async (HttpContext context) =>
            {
                // Custom login logic
            });
        });
        
        return app;
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

**Linbik.Server** - Secure server-side authentication for enterprise applications.
