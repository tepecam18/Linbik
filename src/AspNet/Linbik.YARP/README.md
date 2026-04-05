# Linbik.YARP

YARP (Yet Another Reverse Proxy) integration for Linbik multi-service authentication. Provides automatic token injection, S2S token provider, and typed S2S HTTP client.

## 📦 Installation

```bash
dotnet add package Linbik.YARP
```

## 🚀 Features

### User-Context Proxy
- **Automatic Token Injection** — Inject service-specific JWT tokens into proxied requests from cookies
- **Cookie-Based Storage** — Integration tokens stored in HttpOnly cookies
- **Per-Service Routing** — `/{packageName}/{**path}` routes to integration service BaseUrl

### S2S (Service-to-Service)
- **IS2STokenProvider** — Token caching, auto-refresh, config-based and dynamic targets
- **IS2SServiceClient** — Full typed HTTP client (GET, POST, PUT, DELETE, PATCH) with automatic S2S token injection
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

// Map S2S proxy endpoints (optional)
app.UseLinbikS2S();
```

### appsettings.json

```json
{
  "Linbik": {
    "LinbikUrl": "https://api.linbik.com",
    "ServiceId": "your-service-guid",
    "ApiKey": "lnbk_your_api_key",
    "S2STargetServices": {
      "payment-gateway": "guid-of-payment-service",
      "courier-service": "guid-of-courier-service"
    },
    "S2SAutoRefresh": true,
    "S2SRefreshThreshold": 0.75
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

## 💻 S2S Communication

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

### IS2STokenProvider

```csharp
public interface IS2STokenProvider
{
    // Config-based (package name)
    Task<string?> GetS2STokenAsync(string packageName, CancellationToken ct = default);
    Task<LinbikS2SIntegration?> GetS2SIntegrationAsync(string packageName, CancellationToken ct = default);

    // Dynamic (service ID) — for callbacks/webhooks
    Task<LinbikS2SIntegration?> GetS2SIntegrationByIdAsync(Guid targetServiceId, CancellationToken ct = default);

    // Cache management
    Task RefreshS2STokensAsync(CancellationToken ct = default);
    void ClearCache();
    TimeSpan? GetTimeUntilExpiry();
}
```

### IS2SServiceClient

Typed HTTP client with automatic S2S token injection and `LBaseResponse<T>` format:

#### Config-Based Targets (Package Name)

```csharp
public class MyController : ControllerBase
{
    private readonly IS2SServiceClient _s2sClient;

    public async Task<IActionResult> SyncWithPayment()
    {
        var result = await _s2sClient.PostAsync<SyncRequest, SyncResponse>(
            "payment-gateway",           // package name from config
            "/api/integration/s2s/sync",
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
    private readonly IS2SServiceClient _s2sClient;

    public async Task<IActionResult> NotifyMerchant(Order order)
    {
        var result = await _s2sClient.PostByIdAsync<PaymentNotification, NotifyResponse>(
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
| POST | `PostAsync<TReq, TRes>` | `PostByIdAsync<TReq, TRes>` |
| PUT | `PutAsync<TReq, TRes>` | `PutByIdAsync<TReq, TRes>` |
| DELETE | `DeleteAsync<TRes>` | `DeleteByIdAsync<TRes>` |
| PATCH | `PatchAsync<TReq, TRes>` | `PatchByIdAsync<TReq, TRes>` |

### YARP Route Configuration (Advanced)

For fine-grained control with YARP reverse proxy routes:

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
- [Linbik.Server](../Linbik.Server/README.md)

## 📄 License

MIT License

**Contact**: info@linbik.com

---

**Version**: 1.2.0  
**Platform**: ASP.NET Core 10.0 (net10.0)  
**Last Updated**: 2 Nisan 2026
