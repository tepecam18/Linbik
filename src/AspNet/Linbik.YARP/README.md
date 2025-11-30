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

For most use cases, use `MapLinbikIntegrationProxy()`:

```csharp
// In Program.cs

// 1. Add services with configuration
builder.Services.AddLinbik(builder.Configuration);
builder.Services.AddLinbikJwtAuth();

// 2. Configure integration services in appsettings.json
// See IntegrationServices Configuration section below

var app = builder.Build();

// 3. Map integration proxy endpoint
app.MapLinbikIntegrationProxy();  // Maps /{packageName}/{**path}
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

3. MapLinbikIntegrationProxy():
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
    // Exchange code for tokens and store in session
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
// Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Add Linbik YARP
    services.AddLinbikYARP(options =>
    {
        options.LinbikServerUrl = "https://linbik.com";
        options.MainServiceId = Guid.Parse("main-service-guid");
        options.MainServiceApiKey = "linbik_api_key";
        options.EnableAutomaticRefresh = true;
    });
    
    // Add YARP with Linbik transforms
    services.AddReverseProxy()
        .LoadFromConfig(Configuration.GetSection("ReverseProxy"))
        .AddTransforms<LinbikTokenTransformFactory>();
    
    services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(1);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });
}

public void Configure(IApplicationBuilder app)
{
    app.UseSession();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapReverseProxy();
        endpoints.MapControllers();
    });
}
```

## 🔄 Token Flow

### Initial Authorization

```
User → /login → Linbik → Authorization Code → /oauth/callback
                                                    ↓
                        ExchangeAuthorizationCodeAsync()
                                                    ↓
                        Store tokens in session:
                        - RefreshToken
                        - Integrations[]:
                            - ServicePackage: "payment-gateway"
                              Token: "jwt_token_1"
                              ExpiresAt: DateTime
                            - ServicePackage: "courier-service"
                              Token: "jwt_token_2"
                              ExpiresAt: DateTime
```

### Request Proxying

```
Client Request → YARP Middleware → LinbikTokenTransform
                                          ↓
                        Read X-Service-Package header
                                          ↓
                        GetTokenForServiceAsync("payment-gateway")
                                          ↓
                        Check cache: expired?
                        - No → Return cached token
                        - Yes → RefreshTokensAsync() → Return new token
                                          ↓
                        Inject Authorization: Bearer {token}
                                          ↓
                        Proxy to target service
```

## 📋 Token Cache Structure

Session storage format:

```json
{
  "linbik_refresh_token": "refresh_abc123...",
  "linbik_refresh_expires": "2025-11-30T12:00:00Z",
  "linbik_tokens": {
    "payment-gateway": {
      "token": "eyJhbGci...",
      "expiresAt": "2025-11-01T13:00:00Z"
    },
    "courier-service": {
      "token": "eyJhbGci...",
      "expiresAt": "2025-11-01T13:00:00Z"
    }
  }
}
```

## 🔒 Security Features

### Automatic Token Refresh
✅ Tokens refresh before expiration (default: 5min before)  
✅ Refresh token stored securely in session  
✅ Failed refresh clears cache and requires re-authentication

### Session Security
✅ HttpOnly cookies prevent XSS attacks  
✅ Secure flag for HTTPS-only cookies  
✅ SameSite=Lax for CSRF protection  
✅ Configurable session timeout (default: 1 hour)

### Token Validation
✅ Expiration check before each use  
✅ Service package name validation  
✅ Automatic cache invalidation on errors

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

// ✅ New way (v2.0+ - Per-service tokens)
var token = await _tokenProvider.GetTokenForServiceAsync("payment-gateway", context);
context.Request.Headers["Authorization"] = $"Bearer {token}";

// Even better: Use YARP transform (automatic)
// No manual token management needed!
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [Migration Guide](../../../MIGRATION_GUIDE.md)
- [Examples](../../../examples/AspNet/AspNet)

## 📄 License

This library is currently a work in progress and is not ready for production use.

**Contact**: info@linbik.com

---

**Version**: 2.0.0 (Multi-Service Token Management)  
**Last Updated**: 1 Kasım 2025
