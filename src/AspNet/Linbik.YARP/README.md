# Linbik.YARP

YARP (Yet Another Reverse Proxy) integration for Linbik multi-service authentication.

## 🚀 Features

### Multi-Service Token Management (v2.0+)
- **Automatic Token Injection** - Inject service-specific JWT tokens into proxied requests
- **Per-Service Token Caching** - Cache tokens by service package name
- **Automatic Refresh** - Refresh expired tokens using refresh token
- **Authorization Code Exchange** - Exchange authorization codes for tokens
- **Backward Compatibility** - Supports legacy single-token mode

### Legacy Features (Deprecated)
- Single token provider (use multi-service provider)
- Manual token management (use automatic refresh)

## 📦 Installation

```bash
dotnet add package Linbik.YARP
```

## 🔧 Configuration

### Basic Setup

```csharp
services.AddLinbikYARP(options =>
{
    options.LinbikServerUrl = "https://linbik.com";
    options.TokenEndpoint = "/oauth/token";
    options.RefreshEndpoint = "/oauth/refresh";
    options.MainServiceId = Guid.Parse("your-main-service-guid");
    options.MainServiceApiKey = "linbik_your_api_key";
    options.EnableAutomaticRefresh = true;
    options.TokenCacheExpirationMinutes = 55;  // Refresh before 60min expiration
});
```

### Simple Proxy Setup (Recommended)

For most use cases, use `UseLinbikYarp()`:

```csharp
// In Program.cs

// 1. Add services with configuration
builder.Services.AddLinbik(builder.Configuration);
builder.Services.AddLinbikJwtAuth();

// 2. Configure integration services in appsettings.json
// See IntegrationServices Configuration section below

var app = builder.Build();

// 3. Map integration proxy endpoint
app.UseLinbikYarp();  // Maps /{packageName}/{**path}
```

**Endpoint Pattern**: `/{packageName}/{path}` routes to the integration service's BaseUrl.

**Example**:
- Request: `GET /payment-gateway/api/charge`
- Routes to: `https://payment.example.com/api/charge`
- With: `Authorization: Bearer {jwt_from_cookie}`

### IntegrationServices Configuration

```json
{
  "YARP": {
    "IntegrationServices": {
      "payment-gateway": {
        "BaseUrl": "https://payment.example.com"
      },
      "survey-service": {
        "BaseUrl": "https://survey.example.com"
      },
      "courier-service": {
        "BaseUrl": "https://courier.example.com"
      }
    }
  }
}
```

### How Cookie-Based JWT Injection Works

```
1. User authenticates → Integration tokens stored in cookies
   Cookie: integration_payment-gateway = "eyJhbG..."
   Cookie: integration_survey-service = "eyJhbG..."

2. Client request → /payment-gateway/api/charge

3. UseLinbikYarp():
   a. Extract {packageName} from URL → "payment-gateway"
   b. Read cookie: integration_payment-gateway
   c. Lookup BaseUrl from IntegrationServices config
   d. Proxy request to BaseUrl with Authorization header

4. Target service receives:
   GET /api/charge
   Authorization: Bearer eyJhbG...
```

### YARP Route Configuration

```json
{
  "ReverseProxy": {
    "Routes": {
      "payment-route": {
        "ClusterId": "payment-cluster",
        "Match": {
          "Path": "/api/payment/{**catch-all}"
        },
        "Transforms": [
          {
            "RequestHeader": "X-Service-Package",
            "Set": "payment-gateway"
          }
        ]
      },
      "courier-route": {
        "ClusterId": "courier-cluster",
        "Match": {
          "Path": "/api/courier/{**catch-all}"
        },
        "Transforms": [
          {
            "RequestHeader": "X-Service-Package",
            "Set": "courier-service"
          }
        ]
      }
    },
    "Clusters": {
      "payment-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "https://payment-gateway.com"
          }
        }
      },
      "courier-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "https://courier-service.com"
          }
        }
      }
    }
  }
}
```

## 💻 Usage

### 1. Token Provider Interface

```csharp
public interface ITokenProvider
{
    // Authorization Methods (v2.0+)
    Task<string?> GetTokenForServiceAsync(string servicePackage, HttpContext context);
    Task ExchangeAuthorizationCodeAsync(string authorizationCode, HttpContext context);
    Task RefreshTokensAsync(HttpContext context);
    void ClearTokenCache(HttpContext context);
    
    // Legacy Methods (Deprecated)
    [Obsolete] Task<string?> GetTokenAsync(HttpContext context);
}
```

### 2. Exchange Authorization Code

After OAuth callback:

```csharp
[HttpGet("/oauth/callback")]
public async Task<IActionResult> Callback(string code)
{
    // Exchange code for tokens and store in cookies
    await _tokenProvider.ExchangeAuthorizationCodeAsync(code, HttpContext);
    
    return RedirectToAction("Dashboard");
}
```

### 3. YARP Transform for Token Injection

```csharp
public class LinbikTokenTransform : RequestTransform
{
    private readonly ITokenProvider _tokenProvider;

    public LinbikTokenTransform(ITokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        // Get target service package from request header
        var servicePackage = context.HttpContext.Request.Headers["X-Service-Package"].ToString();
        
        if (!string.IsNullOrEmpty(servicePackage))
        {
            // Get cached token for this service
            var token = await _tokenProvider.GetTokenForServiceAsync(
                servicePackage, 
                context.HttpContext
            );
            
            if (!string.IsNullOrEmpty(token))
            {
                // Inject token into Authorization header
                context.ProxyRequest.Headers.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }
    }
}
```

Register transform:

```csharp
services.AddReverseProxy()
    .LoadFromConfig(Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform<LinbikTokenTransform>();
    });
```

### 4. Complete Example

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Linbik services
builder.Services.AddLinbik(builder.Configuration);
builder.Services.AddLinbikJwtAuth(builder.Configuration);

// Add YARP with Linbik transforms
builder.Services.AddLinbikYarp(builder.Configuration);

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Map Linbik endpoints (login, logout, refresh)
app.UseLinbikJwtAuth();

// Map integration proxy (automatic token injection from cookies)
app.UseLinbikYarp();

app.MapControllers();

app.Run();
```

## 🔄 Token Flow

### Initial Authorization

```
User → /linbik/login → Linbik → Authorization Code → /linbik/login (callback)
                                                            ↓
                            ExchangeAuthorizationCodeAsync()
                                                            ↓
                            Store tokens in cookies:
                            - Cookie: linbikRefreshToken = "refresh_abc..."
                            - Cookie: integration_payment-gateway = "jwt_token_1"
                            - Cookie: integration_courier-service = "jwt_token_2"
```

### Request Proxying

```
Client Request → UseLinbikYarp()
                        ↓
        Extract {packageName} from URL path
                        ↓
        Read cookie: integration_{packageName}
                        ↓
        Lookup BaseUrl from IntegrationServices config
                        ↓
        Inject Authorization: Bearer {token}
                        ↓
        Proxy to target service
```

## 📋 Cookie Storage Structure

Cookies format:

```
Cookie: linbikRefreshToken = "refresh_abc123..." (HttpOnly, Secure, 14 days)
Cookie: integration_payment-gateway = "eyJhbGci..." (HttpOnly, Secure, 1 hour)
Cookie: integration_courier-service = "eyJhbGci..." (HttpOnly, Secure, 1 hour)
```

## 🔒 Security Features

### Cookie-Based Token Storage
✅ Tokens stored in HttpOnly cookies (prevents XSS)  
✅ Secure flag for HTTPS-only transmission  
✅ SameSite=None for cross-origin requests  
✅ Short expiration for integration tokens (1 hour)  
✅ Longer expiration for refresh token (14 days)

### Automatic Token Refresh
✅ Refresh tokens before expiration  
✅ Refresh token stored securely in HttpOnly cookie  
✅ Failed refresh clears cookies and requires re-authentication

### Token Validation
✅ Expiration check on each request  
✅ Service package name validation  
✅ Automatic cookie clearing on errors

## 🎯 Use Cases

### 1. E-Commerce Platform

```
MyShop (Main Service)
  ├── Payment Gateway (Integration Service)
  ├── Courier Service (Integration Service)
  └── Notification Service (Integration Service)

User makes purchase:
1. MyShop receives payment request
2. YARP routes to /api/payment/charge
3. LinbikTokenTransform injects payment-gateway JWT
4. Payment Gateway processes payment
5. MyShop routes to /api/courier/ship
6. LinbikTokenTransform injects courier-service JWT
7. Courier Service creates shipment
```

### 2. Microservices Architecture

```
API Gateway (YARP + Linbik)
  ├── User Service (Integration Service)
  ├── Order Service (Integration Service)
  ├── Inventory Service (Integration Service)
  └── Analytics Service (Integration Service)

Each microservice gets its own JWT token with specific claims.
```

## 🔄 Migration from v1.x

```csharp
// ❌ Old way (v1.x - Single token)
var token = await _tokenProvider.GetTokenAsync(context);
context.Request.Headers["Authorization"] = $"Bearer {token}";

// ✅ New way (v2.0+ - Per-service tokens from cookies)
var token = await _tokenProvider.GetTokenForServiceAsync("payment-gateway", context);
context.Request.Headers["Authorization"] = $"Bearer {token}";

// Even better: Use UseLinbikYarp (automatic)
// No manual token management needed!
// Tokens are read from cookies and injected automatically.
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.Core](../Linbik.Core/README.md)
- [Linbik.JwtAuthManager](../Linbik.JwtAuthManager/README.md)
- [Linbik.Server](../Linbik.Server/README.md)

## 📄 License

This library is currently a work in progress and is not ready for production use.

**Contact**: info@linbik.com

---

**Version**: 2.2.0  
**Last Updated**: 5 Aralık 2025
