# Linbik AspNet - Complete Web Application

A comprehensive ASP.NET Core web application demonstrating the complete Linbik Authentication Framework implementation with real-world features and best practices.

## 🚀 Overview

This project showcases a production-ready web application built with Linbik Authentication Framework. It includes user authentication, app-to-app authentication, YARP reverse proxy integration, and comprehensive security features.

## 📦 Project Structure

```
Linbik.AspNet/
├── Controllers/           # API Controllers
│   ├── AuthController.cs  # Authentication endpoints
│   ├── UserController.cs  # User management
│   ├── AppController.cs   # App authentication
│   └── HealthController.cs # Health checks
├── Models/                # Data models
│   ├── Auth/             # Authentication models
│   ├── User/             # User-related models
│   └── Common/           # Shared models
├── Services/              # Business logic services
│   ├── AuthService.cs     # Authentication logic
│   ├── UserService.cs     # User operations
│   └── TokenService.cs    # Token management
├── Middleware/            # Custom middleware
│   ├── LoggingMiddleware.cs # Request logging
│   ├── SecurityMiddleware.cs # Security headers
│   └── RateLimitingMiddleware.cs # Rate limiting
├── Extensions/            # Service extensions
│   ├── ServiceCollectionExtensions.cs # DI setup
│   └── ApplicationBuilderExtensions.cs # App configuration
├── Configuration/         # Configuration classes
│   ├── LinbikOptions.cs  # Linbik configuration
│   └── AppSettings.cs    # Application settings
├── Program.cs             # Application entry point
├── appsettings.json       # Configuration file
└── READMEnew.md           # This documentation
```

## 🔧 Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or VS Code
- SQL Server LocalDB (for development)
- Basic understanding of ASP.NET Core and Linbik

## 🚀 Quick Start

### 1. Clone and Setup

```bash
git clone https://github.com/your-org/linbik.git
cd linbik/src/AspNet
dotnet restore
dotnet build
```

### 2. Configuration

The `appsettings.json` is already configured with:

- **Linbik Core**: Basic authentication setup
- **JWT Auth**: User authentication with PKCE
- **Server Auth**: App-to-app authentication
- **YARP Integration**: Reverse proxy setup
- **Logging**: Serilog configuration
- **CORS**: Cross-origin resource sharing
- **Rate Limiting**: API protection
- **Health Checks**: Monitoring endpoints

### 3. Run the Application

```bash
dotnet run
```

**Available Endpoints:**
- **Swagger UI**: https://localhost:5001/swagger
- **API Base**: https://localhost:5001/api
- **Health Checks**: https://localhost:5001/health
- **Linbik Auth**: https://localhost:5001/linbik

## 🏗️ Architecture

### Service Registration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Linbik Core
var linbikBuilder = builder.Services.AddLinbik(options =>
{
    options.Version = LinbikVersion.Dev2025;
    options.AppIds = new[] { "webapi", "mobile", "desktop", "admin" };
    options.AllowAllApp = false;
});

// Add JWT Authentication
linbikBuilder.AddJwtAuth(jwtOptions =>
{
    jwtOptions.PrivateKey = builder.Configuration["Linbik:JwtAuth:PrivateKey"];
    jwtOptions.PkceEnabled = true;
    jwtOptions.AccessTokenExpiration = 15;
    jwtOptions.RefreshTokenExpiration = 15;
});

// Add Server Authentication
linbikBuilder.AddLinbikServer(serverOptions =>
{
    serverOptions.PrivateKey = builder.Configuration["Linbik:Server:PrivateKey"];
    serverOptions.AccessTokenExpiration = 60;
});

// Add YARP Integration
linbikBuilder.AddLinbikYARP(yarpOptions =>
{
    yarpOptions.PrivateKey = builder.Configuration["Linbik:YARP:PrivateKey"];
    yarpOptions.PrefixPath = "/api";
});

// Add custom services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
```

### Middleware Pipeline

```csharp
var app = builder.Build();

// Security middleware
app.UseSecurityHeaders();
app.UseRateLimiting();

// Linbik middleware
app.UseLinbik();
app.UseLinbikServer();
app.UseLinbikYARP();

// Standard middleware
app.UseAuthentication();
app.UseAuthorization();
app.UseCors("DefaultPolicy");

// Custom middleware
app.UseLogging();
app.UseHealthChecks("/health");

// Endpoints
app.MapControllers();
app.MapReverseProxy();
```

## 📋 API Endpoints

### Authentication Endpoints

#### 1. User Authentication
```http
# PKCE Start
POST /linbik/pkce-start

# User Login
GET /linbik/login?token={jwt}&verifier={pkce}&route={app}

# Refresh Token
POST /linbik/refresh-token

# Logout
POST /linbik/logout
```

#### 2. App Authentication
```http
# App Login
POST /linbik/app-login
Content-Type: application/json

{
  "appId": "123e4567-e89b-12d3-a456-426614174000",
  "key": "app-secret-key"
}
```

#### 3. API Endpoints
```http
# User Profile
GET /api/users/profile
Authorization: Bearer {token}

# Update Profile
PUT /api/users/profile
Authorization: Bearer {token}

# Admin Users
GET /api/admin/users
Authorization: Bearer {token}
```

## 🔐 Security Features

### JWT Token Security

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly ICurrentActor _currentActor;
    
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        if (!_currentActor.IsAuthenticated)
            return Unauthorized();
            
        var profile = await _userService.GetProfileAsync(_currentActor.UserGuid.Value);
        return Ok(profile);
    }
}
```

### Role-Based Authorization

```csharp
[HttpGet("admin/users")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> GetAllUsers()
{
    var users = await _userService.GetAllUsersAsync();
    return Ok(users);
}
```

### Tenant Isolation

```csharp
[HttpGet("tenant-data")]
public async Task<IActionResult> GetTenantData()
{
    var tenantId = _currentActor.TenantId;
    var data = await _dataService.GetDataForTenantAsync(tenantId);
    
    return Ok(new
    {
        TenantId = tenantId,
        Data = data,
        UserType = _currentActor.UserType.ToString()
    });
}
```

## 🌐 Multi-Tenant Support

### Tenant Configuration

```csharp
// Each tenant has isolated configuration
var tenantConfig = new Dictionary<string, TenantOptions>
{
    ["company_a"] = new TenantOptions
    {
        AppIds = new[] { "webapi", "mobile" },
        PublicKey = "company_a_public_key",
        AllowedDomains = new[] { "company-a.com" }
    },
    ["company_b"] = new TenantOptions
    {
        AppIds = new[] { "desktop", "admin" },
        PublicKey = "company_b_public_key",
        AllowedDomains = new[] { "company-b.com" }
    }
};
```

### Tenant-Specific Data Access

```csharp
public class TenantDataService
{
    public async Task<IEnumerable<DataModel>> GetDataForTenantAsync(string tenantId)
    {
        // Tenant-specific data access
        switch (tenantId)
        {
            case "company_a":
                return await _context.CompanyAData.ToListAsync();
            case "company_b":
                return await _context.CompanyBData.ToListAsync();
            default:
                throw new ArgumentException($"Invalid tenant: {tenantId}");
        }
    }
}
```

## 📊 Monitoring and Logging

### Serilog Configuration

```csharp
// Program.cs
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();
```

### Structured Logging

```csharp
public class AuthService
{
    private readonly ILogger<AuthService> _logger;
    
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        _logger.LogInformation("Login attempt for user {Username} from {IPAddress}", 
            request.Username, GetClientIP());
        
        try
        {
            var result = await AuthenticateUserAsync(request);
            _logger.LogInformation("User {Username} logged in successfully", request.Username);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for user {Username}", request.Username);
            throw;
        }
    }
}
```

### Health Checks

```csharp
[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        });
    }
    
    [HttpGet("db")]
    public async Task<IActionResult> CheckDatabase()
    {
        try
        {
            // Database health check
            await _context.Database.CanConnectAsync();
            return Ok(new { Status = "Healthy", Database = "Connected" });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { Status = "Unhealthy", Database = ex.Message });
        }
    }
}
```

## 🔧 Customization Examples

### Custom Middleware

```csharp
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        
        await _next(context);
    }
}
```

### Rate Limiting

```csharp
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = GetClientId(context);
        var cacheKey = $"rate_limit_{clientId}";
        
        if (_cache.TryGetValue(cacheKey, out int requestCount))
        {
            if (requestCount >= 100) // 100 requests per minute
            {
                _logger.LogWarning("Rate limit exceeded for client {ClientId}", clientId);
                context.Response.StatusCode = 429; // Too Many Requests
                await context.Response.WriteAsync("Rate limit exceeded");
                return;
            }
            
            _cache.Set(cacheKey, requestCount + 1, TimeSpan.FromMinutes(1));
        }
        else
        {
            _cache.Set(cacheKey, 1, TimeSpan.FromMinutes(1));
        }
        
        await _next(context);
    }
}
```

## 🧪 Testing

### Unit Testing

```csharp
[TestFixture]
public class AuthServiceTests
{
    private AuthService _authService;
    private Mock<IUserService> _mockUserService;
    private Mock<ITokenService> _mockTokenService;
    
    [SetUp]
    public void Setup()
    {
        _mockUserService = new Mock<IUserService>();
        _mockTokenService = new Mock<ITokenService>();
        _authService = new AuthService(_mockUserService.Object, _mockTokenService.Object);
    }
    
    [Test]
    public async Task LoginAsync_ValidCredentials_ReturnsLoginResponse()
    {
        // Arrange
        var request = new LoginRequest { Username = "testuser", Password = "testpass" };
        var user = new User { Id = Guid.NewGuid(), Username = "testuser" };
        var token = "valid-jwt-token";
        
        _mockUserService.Setup(x => x.ValidateCredentialsAsync(request))
            .ReturnsAsync(user);
        _mockTokenService.Setup(x => x.GenerateTokenAsync(user))
            .ReturnsAsync(token);
        
        // Act
        var result = await _authService.LoginAsync(request);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(token, result.Token);
        Assert.AreEqual(user.Id, result.User.Id);
    }
}
```

### Integration Testing

```csharp
[TestFixture]
public class AuthenticationIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    
    [SetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
            });
        _client = _factory.CreateClient();
    }
    
    [Test]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "testuser",
            Password = "testpass"
        };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        // Assert
        Assert.IsTrue(response.IsSuccessStatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Data.Token);
    }
}
```

## 🚀 Deployment

### Docker Support

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Linbik.AspNet.csproj", "./"]
RUN dotnet restore "Linbik.AspNet.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Linbik.AspNet.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Linbik.AspNet.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Linbik.AspNet.dll"]
```

### Environment Configuration

```bash
# Production environment variables
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://+:80;https://+:443
export Linbik__JwtAuth__PrivateKey=your-production-jwt-key
export Linbik__Server__PrivateKey=your-production-server-key
export Linbik__YARP__PrivateKey=your-production-yarp-key
export ConnectionStrings__DefaultConnection=your-production-connection-string
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: linbik-aspnet
spec:
  replicas: 3
  selector:
    matchLabels:
      app: linbik-aspnet
  template:
    metadata:
      labels:
        app: linbik-aspnet
    spec:
      containers:
      - name: linbik-aspnet
        image: linbik/aspnet:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: Linbik__JwtAuth__PrivateKey
          valueFrom:
            secretKeyRef:
              name: linbik-secrets
              key: jwt-private-key
```

## 📚 Best Practices Demonstrated

### 1. **Security**
- JWT token validation with PKCE
- Role-based authorization
- Tenant isolation
- Rate limiting
- Security headers
- Secure configuration

### 2. **Performance**
- Async/await patterns
- Efficient dependency injection
- Structured logging
- Health monitoring
- Rate limiting

### 3. **Maintainability**
- Clean architecture
- Separation of concerns
- Comprehensive testing
- Clear documentation
- Configuration management

### 4. **Scalability**
- Multi-tenant support
- YARP reverse proxy
- Load balancing ready
- Health checks
- Monitoring

## 🔍 Troubleshooting

### Common Issues

#### 1. **JWT Token Validation Fails**
```bash
# Check configuration
dotnet user-secrets list

# Verify private key format
# Ensure PKCE is properly configured
# Check token expiration settings
```

#### 2. **Authentication Not Working**
```bash
# Check service registration
dotnet run --environment Development

# Verify middleware order
# Check authorization attributes
# Review CORS configuration
```

#### 3. **YARP Proxy Issues**
```bash
# Check YARP configuration
# Verify cluster addresses
# Check routing rules
# Review health checks
```

#### 4. **Performance Issues**
```bash
# Monitor memory usage
dotnet-counters monitor

# Check logging levels
# Verify async patterns
# Review rate limiting
```

## 📞 Support

### Getting Help

- **Documentation**: Check Linbik framework documentation
- **Examples**: Use this project as reference
- **Issues**: Create GitHub issues for bugs
- **Community**: Join Linbik discussions

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

---

**Linbik AspNet** - A complete example of Linbik Authentication Framework implementation in a production-ready web application.

*Use this project as a reference for implementing Linbik in your own ASP.NET Core applications.*
