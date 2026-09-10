# Linbik.YARP

YARP (Yet Another Reverse Proxy) integration for Linbik multi-service authentication. Provides automatic token injection, apps token provider, and typed apps HTTP client.

## 📦 Installation

```bash
dotnet add package Linbik.YARP
```

## 🚀 Features

### User-Context Proxy
- **Automatic Token Injection** — Inject service-specific JWT tokens into proxied requests from cookies
- **Cookie-Based Storage** — Integration tokens stored in HttpOnly cookies
- **Per-Service Routing** — `/{packageName}/{**path}` routes to integration service BaseUrl

### Apps (Service-to-Service)
- **IApplicationTokenProvider** — Token caching, auto-refresh, config-based and dynamic targets
- **IApplicationServiceClient** — Full typed HTTP client (GET, POST, PUT, DELETE, PATCH) with automatic apps token injection
- **LBaseResponse\<T\> Enforcement** — Consistent response format
- **Role-Based Tokens** — `Service` (service-to-service) and `Linbik` (platform) roles

## 🔧 Configuration

### Fluent Builder (Recommended)

```csharp
// In Program.cs
builder.Services.AddLinbik()
    .AddLinbikJwtAuth()
    .AddLinbikYarp();

var app = builder.Build();
app.EnsureLinbik();

// Map user-context integration proxy: /{packageName}/{**path}
app.UseLinbikYarp();

// Map apps proxy endpoints (optional): /{routePrefix}/{packageName}/{**path}
// routePrefix defaults to "app" (UseLinbikApplication(string routePrefix = "app"))
app.UseLinbikApplication();
```

### appsettings.json

```json
{
  "Linbik": {
    "LinbikUrl": "https://api.linbik.com",
    "ServiceId": "your-service-guid",
    "ApiKey": "lnbk_your_api_key",
    "AppsTargetServices": {
      "payment-gateway": "guid-of-payment-service",
      "courier-service": "guid-of-courier-service"
    },
    "AppsAutoRefresh": true,
    "AppsRefreshThreshold": 0.75
  },
  "YARP": {
    "IntegrationServices": {
      "payment-gateway": {
        "BaseUrl": "https://payment.example.com"
      },
      "survey-service": {
        "BaseUrl": "https://survey.example.com"
      }
    }
  }
}
```

## 💻 User-Context Proxy (UseLinbikYarp)

### How It Works

```
1. User authenticates → Integration tokens stored in cookies
   Cookie: integration_payment-gateway = "eyJhbG..."
   Cookie: integration_survey-service = "eyJhbG..."

2. Client request → /payment-gateway/api/charge

3. UseLinbikYarp():
   a. Extract {packageName} from URL → "payment-gateway"
   b. Read cookie: integration_payment-gateway
   c. Lookup BaseUrl from YARP:IntegrationServices config
   d. Proxy request with Authorization: Bearer {token}

4. Target service receives:
   GET /api/charge
   Authorization: Bearer eyJhbG...
```

### Cookie Storage

```
Cookie: linbikRefreshToken = "refresh_abc..." (HttpOnly, Secure, 30 days)
Cookie: integration_payment-gateway = "eyJhbGci..." (HttpOnly, Secure, 1 hour)
Cookie: integration_courier-service = "eyJhbGci..." (HttpOnly, Secure, 1 hour)
```

## 💻 Application (Apps / Service-to-Service) Communication

### ITokenProvider

```csharp
public interface ITokenProvider
{
    Task<LinbikTokenResponse?> GetMultiServiceTokenAsync(
        string baseUrl, string authorizationCode, string apiKey);
    Task<LinbikTokenResponse?> RefreshTokensAsync(
        string baseUrl, string refreshToken, string apiKey, string serviceId);
    Task<string?> GetIntegrationTokenAsync(string integrationServicePackage);
    void CacheTokenResponse(LinbikTokenResponse tokenResponse);
    void ClearCache();
}
```

### IApplicationTokenProvider

Manages PASETO application token caching, automatic refresh, and thread-safe access:

```csharp
public interface IApplicationTokenProvider
{
    // Config-based (package name)
    Task<string?> GetApplicationTokenAsync(string integrationPackageName, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetApplicationTokensAsync(
        IEnumerable<string> integrationPackageNames, CancellationToken cancellationToken = default);
    Task<LinbikApplicationIntegration?> GetApplicationIntegrationAsync(
        string integrationPackageName, CancellationToken cancellationToken = default);

    // Dynamic (service ID) — for callbacks/webhooks, no config entry required
    Task<LinbikApplicationIntegration?> GetApplicationIntegrationByIdAsync(
        Guid targetServiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, LinbikApplicationIntegration>> GetApplicationIntegrationsByIdAsync(
        IEnumerable<Guid> targetServiceIds, CancellationToken cancellationToken = default);

    // Cache management
    Task RefreshApplicationTokensAsync(CancellationToken cancellationToken = default);
    void ClearCache();
    TimeSpan? GetTimeUntilExpiry();
}
```

### IApplicationServiceClient

Typed HTTP client with automatic application-token injection and `LBaseResponse<T>` format:

#### Config-Based Targets (Package Name)

```csharp
public class MyController : ControllerBase
{
    private readonly IApplicationServiceClient _applicationClient;

    public async Task<IActionResult> SyncWithPayment()
    {
        var result = await _applicationClient.PostAsync<SyncRequest, SyncResponse>(
            "payment-gateway",           // package name from config
            "/api/integration/app/sync",
            new SyncRequest { EntityType = "order", EntityId = "123" }
        );

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.FriendlyMessage);
    }
}
```

#### Dynamic Targets (Service ID) — Callbacks/Webhooks

```csharp
public class PaymentController : ControllerBase
{
    private readonly IApplicationServiceClient _applicationClient;

    public async Task<IActionResult> NotifyMerchant(Order order)
    {
        var result = await _applicationClient.PostByIdAsync<PaymentNotification, NotifyResponse>(
            order.MerchantLinbikServiceId,   // dynamic service ID
            "/api/webhooks/payment",
            new PaymentNotification
            {
                OrderId = order.Id.ToString(),
                Status = "completed",
                Amount = order.Amount
            }
        );

        return result.IsSuccess ? Ok() : StatusCode(500);
    }
}
```

### Available HTTP Methods

| Method | Config-Based | Dynamic (by ID) |
|--------|-------------|-----------------|
| GET | `GetAsync<TResponse>` | `GetByIdAsync<TResponse>` |
| POST | `PostAsync<TReq, TRes>` (+ `PostAsync<TReq>` without response) | `PostByIdAsync<TReq, TRes>` (+ `PostByIdAsync<TReq>` without response) |
| PUT | `PutAsync<TReq, TRes>` | `PutByIdAsync<TReq, TRes>` |
| DELETE | `DeleteAsync<TRes>` (+ `DeleteAsync` without response) | `DeleteByIdAsync<TRes>` (+ `DeleteByIdAsync` without response) |
| PATCH | `PatchAsync<TReq, TRes>` | `PatchByIdAsync<TReq, TRes>` |

### Application Proxy Endpoints (UseLinbikApplication)

`UseLinbikApplication(string routePrefix = "app")` maps one route per configured `IntegrationServices` entry: `/{routePrefix}/{packageName}/{**path}` → `{TargetBaseUrl}{TargetPath}/{path}`, injecting the cached application PASETO token as `Authorization: Bearer {token}` (no user context/cookie required). No local cache hit means a `503 service_unavailable` response.

`YARPOptions.ApplicationTimeoutSeconds` configures the `IApplicationServiceClient`'s HTTP timeout.

### YARP Route Configuration (Advanced)

For fine-grained control with YARP reverse proxy routes (user-context token injection via `ITokenProvider`):

```csharp
// Add Linbik token transform to YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig()
    .AddLinbikTokenTransform();
```

## 📖 Documentation

- [Full Documentation](https://github.com/tepecam18/Linbik)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [Examples](../../../examples/AspNet/AspNet)
- [Linbik.Core](../Linbik.Core/README.md)
- [Linbik.JwtAuthManager](../Linbik.JwtAuthManager/README.md)
- [Linbik.PasetoAuthManager](../Linbik.PasetoAuthManager/README.md)
- [Linbik.Server](../Linbik.Server/README.md)
- [Linbik.Slices](../Linbik.Slices/README.md)

## 📄 License

MIT License

**Contact**: info@linbik.com

---

**Version**: 1.2.0  
**Platform**: ASP.NET Core 10.0 (net10.0)  
**Last Updated**: 9 Eylül 2026
