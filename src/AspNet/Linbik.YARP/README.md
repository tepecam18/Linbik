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

// 1. Add services with configuration (fluent chain)
builder.Services.AddLinbik(builder.Configuration)
    .AddLinbikJwtAuth();

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

// Add Linbik services (fluent chain)
builder.Services.AddLinbik(builder.Configuration)
    .AddLinbikJwtAuth(builder.Configuration)
    .AddLinbikYarp(builder.Configuration);

var app = builder.Build();

app.EnsureLinbik();
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

## � Service-to-Service (S2S) Communication (v2.4+)

### Overview

S2S allows services to communicate directly without user context. Use cases:
- **Webhooks/Callbacks**: Payment Gateway → Merchant notification
- **Background Sync**: Inventory → Order synchronization
- **Health Checks**: Service monitoring between services
- **Batch Processing**: Data transfer between services

### Key Difference from User-Context

| Aspect | User-Context | S2S |
|--------|-------------|-----|
| Token Type | User JWT (from cookie) | S2S JWT (from API) |
| Claims | UserId, Username, DisplayName | SourceServiceId, SourcePackageName, Role |
| Use Case | User-initiated requests | Service-initiated requests |
| Attribute | `[LinbikUserServiceAuthorize]` | `[LinbikS2SAuthorize]` / `[LinbikS2SAuthorize("Service")]` / `[LinbikS2SAuthorize("Linbik")]` |

### S2S Token Flow

```
1. Service A needs to call Service B
2. Service A → Linbik: POST /auth/s2s-token (ApiKey + TargetServiceIds)
3. Linbik → Service A: S2S JWT tokens for each target
4. Service A → Service B: Request with S2S JWT
5. Service B validates JWT with [LinbikS2SAuthorize]
```

### Configuration

```json
{
  "Linbik": {
    "ServiceId": "your-service-guid",
    "ApiKey": "linbik_your_api_key",
    "S2STokenEndpoint": "/auth/s2s-token",
    "S2STokenLifetimeMinutes": 60,
    "S2SAutoRefresh": true,
    "S2SRefreshThreshold": 0.75,
    "S2STargetServices": {
      "payment-gateway": "guid-of-payment-service",
      "courier-service": "guid-of-courier-service"
    },
    "YARP": {
      "SourcePackageName": "my-service",
      "S2STimeoutSeconds": 30,
      "IntegrationServices": {
        "payment-gateway": {
          "TargetBaseUrl": "https://payment.example.com"
        }
      }
    }
  }
}
```

### Using IS2SServiceClient

The `IS2SServiceClient` provides typed HTTP methods with:
- ✅ Automatic S2S token injection
- ✅ `LBaseResponse<T>` enforcement
- ✅ Token caching and auto-refresh
- ✅ Both config-based and dynamic targets

#### Config-Based Targets (Package Name)

```csharp
public class MyController : ControllerBase
{
    private readonly IS2SServiceClient _s2sClient;

    // Call by package name (must be in config)
    public async Task<IActionResult> SyncWithPayment()
    {
        var result = await _s2sClient.PostAsync<SyncRequest, SyncResponse>(
            "payment-gateway",  // package name from config
            "/api/integration/s2s/sync",
            new SyncRequest { EntityType = "order", EntityId = "123" }
        );

        if (!result.IsSuccess)
        {
            return BadRequest(result.FriendlyMessage);
        }

        return Ok(result.Data);
    }
}
```

#### Dynamic Targets (Service ID) - Callbacks/Webhooks

For scenarios where target service is not pre-configured (e.g., callbacks):

```csharp
public class PaymentController : ControllerBase
{
    private readonly IS2SServiceClient _s2sClient;

    // Notify merchant about payment completion
    public async Task<IActionResult> NotifyMerchant(Order order)
    {
        // Merchant's service ID is stored in order (not in config!)
        var merchantServiceId = order.MerchantLinbikServiceId;

        var result = await _s2sClient.PostByIdAsync<PaymentNotification, NotifyResponse>(
            merchantServiceId,  // dynamic service ID
            "/api/webhooks/payment",
            new PaymentNotification 
            { 
                OrderId = order.Id.ToString(),
                Status = "completed",
                Amount = order.Amount 
            }
        );

        // ServiceUrl fetched automatically from Linbik
        return result.IsSuccess ? Ok() : StatusCode(500);
    }
}
```

### Protecting S2S Endpoints

Use `[LinbikS2SAuthorize]` attribute on endpoints that receive S2S requests. With optional role parameter, you can restrict access to specific token types:

```csharp
[ApiController]
[Route("api/integration")]
public class IntegrationController : ControllerBase
{
    // S2S endpoint - accepts ANY S2S token (service or platform)
    [LinbikS2SAuthorize]
    [HttpPost("s2s/sync")]
    public IActionResult S2SSync([FromBody] SyncRequest request)
    {
        var sourceServiceId = User.FindFirst("source_service_id")?.Value;
        var sourcePackageName = User.FindFirst("source_package_name")?.Value;
        var role = User.FindFirst("role")?.Value; // "Service" or "Linbik"

        return Ok(new { success = true, sourceServiceId, sourcePackageName });
    }

    // S2S webhook - only accepts service-to-service tokens (role=Service)
    [LinbikS2SAuthorize("Service")]
    [HttpPost("s2s/webhook/{eventType}")]
    public IActionResult S2SWebhook(string eventType, [FromBody] WebhookPayload payload)
    {
        var sourceServiceId = User.FindFirst("source_service_id")?.Value;
        return Ok(new { processed = true, eventType });
    }

    // Platform event - only accepts platform tokens (role=Linbik)
    // Use for: key rotation, integration lifecycle, admin commands
    [LinbikS2SAuthorize("Linbik")]
    [HttpPost("s2s/platform-event")]
    public IActionResult OnPlatformEvent([FromBody] PlatformEventPayload payload)
    {
        // Only Linbik platform can call this endpoint
        return Ok(new { processed = true });
    }
}
```

### S2S Claim Types

Claims available in S2S JWT tokens:

```
token_type = "s2s"
source_service_id = "guid-of-calling-service"
source_package_name = "calling-service-package"
role = "Service" | "Linbik"          // NEW: distinguishes service vs platform tokens
iat = issued at timestamp
exp = expiration timestamp
iss = "Linbik"
aud = "target-service-guid"
```

> 🛡️ **Cross-Scheme Protection**: `OnTokenValidated` events in Linbik.Server ensure that S2S tokens cannot be used on `[LinbikUserServiceAuthorize]` endpoints and vice versa. This is enforced via `token_type` claim validation.

### LinbikProxyPolicy

`AddCommonYarpServices()` automatically registers the `LinbikProxyPolicy` authorization policy as `RequireAuthenticatedUser()`. This policy is referenced in YARP route configurations and does not need to be defined by the consumer application.

### IS2STokenProvider API

For advanced scenarios, you can use the token provider directly:

```csharp
public interface IS2STokenProvider
{
    // Config-based (package name)
    Task<string?> GetS2STokenAsync(string packageName, ...);
    Task<LinbikS2SIntegration?> GetS2SIntegrationAsync(string packageName, ...);
    
    // Dynamic (service ID) - for callbacks
    Task<LinbikS2SIntegration?> GetS2SIntegrationByIdAsync(Guid targetServiceId, ...);
    
    // Cache management
    Task RefreshS2STokensAsync(...);
    void ClearCache();
    TimeSpan? GetTimeUntilExpiry();
}
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

**Version**: 2.4.0  
**Last Updated**: 28 Şubat 2026
