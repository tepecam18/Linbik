# Linbik Libraries Usage Guide - AspNet Project

This document explains how to use Linbik libraries in the AspNet project.

## 📋 Table of Contents

- [Project Structure](#project-structure)
- [Installation](#installation)
- [Configuration](#configuration)
- [Service Registration](#service-registration)
- [Authentication](#authentication)
- [Authorization](#authorization)
- [Middleware Usage](#middleware-usage)
- [API Endpoints](#api-endpoints)
- [Examples](#examples)

## 🏗️ Project Structure

The AspNet project uses the following Linbik libraries:

- **Linbik.Core**: Core services and middleware
- **Linbik.JwtAuthManager**: JWT-based authentication
- **Linbik.Server**: Server-side application authentication
- **Linbik.YARP**: Reverse proxy and routing

## 📦 Installation

### 1. Project References

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\src\AspNet\Linbik.JwtAuthManager\Linbik.JwtAuthManager.csproj" />
  <ProjectReference Include="..\..\..\src\AspNet\Linbik.Server\Linbik.Server.csproj" />
  <ProjectReference Include="..\..\..\src\AspNet\Linbik.YARP\Linbik.YARP.csproj" />
</ItemGroup>
```

### 2. Using Directives

```csharp
using Linbik.Core.Extensions;
using Linbik.JwtAuthManager.Extensions;
using Linbik.Server.Extensions;
using Linbik.Server.Interfaces;
using Linbik.YARP.Extensions;
```

## ⚙️ Configuration

### appsettings.json

```json
{
  "Linbik": {
    "Version": "dev2025",
    "AllowAllApp": true,
    "AppIds": [
      "01971792-3470-7307-873b-46937e7fe682",
      "01956070-9f16-7791-a93e-c09c3735f3ae",
      "556b3898-6cbb-4488-84d8-4ce9236acabc"
    ],
    "JwtAuth": {
      "PrivateKey": "your-secure-private-key",
      "PkceEnabled": false,
      "Routes": {
        "mobile": "https://localhost.com/mobil",
        "web": "https://localhost.com/web"
      }
    },
    "Server": {
      "PrivateKey": "your-secure-server-key"
    },
    "Yarp": [
      {
        "RouteId": "route1",
        "ClusterId": "cluster1",
        "PrefixPath": "webhook",
        "Clusters": [
          {
            "Name": "webhook1",
            "Address": "https://webhook.leptudo.com/db9574b5-0537-4e68-a2a5-9ca26cc7f69c"
          }
        ]
      }
    ]
  }
}
```

## 🔧 Service Registration

### 1. Basic Linbik Services

```csharp
// Program.cs - Service Registration Section
builder.Services
    .AddLinbik() // Core Linbik services
    .AddJwtAuth(true) // JWT authentication (PKCE enabled)
    .AddLinbikServer() // Server services
    .AddProxy(); // Proxy services
```

### 2. Custom Configuration Service Registration

```csharp
// Alternative: With custom configuration
builder.Services.AddLinbik(conf =>
{
    conf.AppIds = new string[] { "app1", "app2" };
    conf.AllowAllApp = false;
    conf.Version = "dev2025";
});
```

### 3. Repository Registration

```csharp
// Register repository service
builder.Services.AddSingleton<ILinbikServerRepository, LinbikServerRepository>();
```

## 🔐 Authentication

### 1. Authentication Schemes Registration

```csharp
// Program.cs - Authentication Section
builder.Services
    .AddAuthentication()
    .AddLinbikScheme(builder.Configuration) // Linbik user scheme
    .AddLinbikAppScheme(builder.Configuration); // Linbik application scheme
```

### 2. Authentication Schemes Description

- **LinbikScheme**: For user authentication
- **LinbikAppScheme**: For application authentication

## 🛡️ Authorization

### 1. Authorization Policies

```csharp
// Program.cs - Authorization Section
builder.Services.AddAuthorization(options =>
{
    // Policy for YARP Linbik applications
    options.AddPolicy("LinbikAppProxyPolicy", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddAuthenticationSchemes("LinbikAppScheme");
    });
});
```

### 2. Policy Usage

```csharp
// Usage in Controller
[Authorize(Policy = "LinbikAppProxyPolicy")]
public class SecureController : ControllerBase
{
    // Secure endpoints
}
```

## 🔄 Middleware Usage

### 1. Middleware Order

```csharp
// Program.cs - Middleware Section
var app = builder.Build();

// Middleware order is important!
app.UseRouting();
app.UseAuthentication(); // First authentication
app.UseAuthorization();  // Then authorization

// Linbik middleware
app.UseLinbikServer(); // Linbik server endpoints
app.UseJwtAuth();      // JWT authentication
app.UseProxy();        // YARP proxy
```

### 2. Middleware Descriptions

- **UseLinbikServer()**: Enables server endpoints like `/linbik/app-login`
- **UseJwtAuth()**: Enables JWT authentication endpoints
- **UseProxy()**: Enables YARP reverse proxy

## 🌐 API Endpoints

### JWT Authentication Endpoints

- `POST /linbik/login` - User login
- `POST /linbik/refresh-token` - Token refresh
- `POST /linbik/logout` - Logout
- `GET /linbik/pkce-start` - PKCE start (if enabled)

### Server Authentication Endpoints

- `POST /linbik/app-login` - Application login

### YARP Proxy Endpoints

- `/{prefixPath}/*` - Configured proxy routes
  - `/webhook/*` → Webhook services
  - `/app/*` → Application services

## 📝 Examples

### 1. Controller Example

```csharp
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "LinbikAppProxyPolicy")]
    public IActionResult Get()
    {
        var user = User.Identity?.Name;
        return Ok($"Hello {user}!");
    }
}
```

### 2. Repository Example

```csharp
// Repositories/LinbikServerRepository.cs
public class LinbikServerRepository : ILinbikServerRepository
{
    public async Task<bool> ValidateAppAsync(string appId, string token)
    {
        // Application validation logic
        // Database checks can be performed here
        return true;
    }
}
```

### 3. Custom Middleware Example

```csharp
// Add custom middleware in Program.cs
app.UseMiddleware<UnauthorizedPayloadMiddleware>();
```

## 🔒 Security

### 1. Key Security

- **PrivateKey**: Use strong keys with at least 64 characters
- **AppIds**: Only add necessary application IDs
- **AllowAllApp**: Set to `false` in production

### 2. Token Management

- Access tokens are short-lived (default 15 minutes)
- Refresh tokens are long-lived (default 15 days)
- Secure code exchange with PKCE support

### 3. CORS and Referer Control

```csharp
// In JWT configuration
"JwtAuth": {
  "RefererControl": true,
  "Routes": {
    "allowed-domain": "https://yourdomain.com"
  }
}
```

## 🚨 Troubleshooting

### 1. Log Levels

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore.Authentication": "Trace",
      "Microsoft.AspNetCore.Authorization": "Trace"
    }
  }
}
```

### 2. Common Errors and Solutions

1. **Configuration Binding Error**
   - Ensure property names are correct
   - Use PascalCase (AllowAllApp, AppIds, PrivateKey)

2. **Authentication Scheme Error**
   - Check that schemes are added in correct order
   - UseAuthentication() first, UseAuthorization() second

3. **Private Key Error**
   - Verify keys are long enough
   - Minimum 64 characters required

4. **Middleware Order Error**
   - UseRouting() → UseAuthentication() → UseAuthorization() → Linbik Middleware

## 🔧 Advanced Configuration

### 1. PKCE Settings

```csharp
// Enable/disable PKCE
builder.Services.AddJwtAuth(true);  // PKCE enabled
builder.Services.AddJwtAuth(false); // PKCE disabled
```

### 2. Token Expiration

```json
{
  "Linbik": {
    "JwtAuth": {
      "AccessTokenExpiration": 15,    // 15 minutes
      "RefreshTokenExpiration": 15    // 15 days
    },
    "Server": {
      "AccessTokenExpiration": 60     // 60 minutes
    }
  }
}
```

### 3. YARP Proxy Configuration

```json
{
  "Linbik": {
    "Yarp": [
      {
        "RouteId": "custom-route",
        "ClusterId": "custom-cluster",
        "PrefixPath": "api",
        "PrivateKey": "route-specific-key",
        "Clusters": [
          {
            "Name": "service1",
            "Address": "https://service1.yourdomain.com"
          },
          {
            "Name": "service2",
            "Address": "https://service2.yourdomain.com"
          }
        ]
      }
    ]
  }
}
```

## 📚 Additional Resources

- [Linbik.Core Documentation](../Linbik.Core/README.md)
- [Linbik.JwtAuthManager Documentation](../Linbik.JwtAuthManager/README.md)
- [Linbik.Server Documentation](../Linbik.Server/README.md)
- [Linbik.YARP Documentation](../Linbik.YARP/README.md)

## 🤝 Support

For issues:
1. Use GitHub Issues
2. Check this documentation
3. Review log files
4. Verify middleware order

---

**Note**: This document covers the usage of Linbik libraries in the AspNet project. For advanced features, refer to the respective project README files.
