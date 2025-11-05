# Linbik.YARP

**Version**: 1.0.0  
**Target Framework**: .NET 9.0  
**Purpose**: API Gateway with automatic token injection for Linbik integration services

> ⚠️ **IMPORTANT**: This library is OPTIONAL. Use only if you need centralized API Gateway routing with automatic token management.

---

## 📋 What is Linbik.YARP?

**Linbik.YARP** is an optional API Gateway built on [YARP (Yet Another Reverse Proxy)](https://microsoft.github.io/reverse-proxy/) that automatically injects integration service tokens into requests.

### Architecture Overview

```
Client App (MyBlog)
       ↓
   [Linbik.YARP Gateway]
       ↓
   Automatic Token Injection
       ↓
Integration Services (Payment Gateway, Courier Service, etc.)
```

### When to Use Linbik.YARP

✅ **Use if you need**:
- Centralized routing to multiple integration services
- Automatic token injection from session
- Unified error handling and retry logic
- Request/response transformation
- Rate limiting and circuit breaker patterns

❌ **Don't use if**:
- You only have 1-2 integration services (direct calls are simpler)
- You want full control over HTTP requests
- Your integration services are not HTTP-based

---

## ✨ Key Features

### 1. Automatic Token Injection

YARP automatically retrieves integration tokens from session and injects them into `Authorization` headers.

```csharp
// Without YARP (manual token management)
var tokens = HttpContext.Session.GetString("_LinbikTokens");
var paymentToken = FindToken(tokens, "payment-gateway");
await httpClient.GetAsync("https://payment-gateway.com/api/charge", 
    new { Authorization = $"Bearer {paymentToken}" });

// With YARP (automatic)
await httpClient.GetAsync("/payment-gateway/api/charge");
// YARP automatically adds: Authorization: Bearer {token}
```

### 2. Dynamic Routing

Routes are configured based on integration service package names.

```json
{
  "ReverseProxy": {
    "Routes": {
      "payment-route": {
        "ClusterId": "payment-cluster",
        "Match": {
          "Path": "/payment-gateway/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "payment-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "https://payment-gateway.com"
          }
        }
      }
    }
  }
}
```

### 3. Token Refresh Logic

Automatically refreshes expired tokens using `Linbik.Core.ILinbikHttpClient`.

```csharp
if (token.ExpiresAt < DateTime.UtcNow)
{
    var refreshToken = HttpContext.Session.GetString("_LinbikRefreshToken");
    var newTokens = await _linbikHttpClient.RefreshTokensAsync(refreshToken);
    UpdateSession(newTokens);
}
```

### 4. Error Handling

Handles 401/403 errors and redirects to login if tokens are invalid.

```csharp
// 401 Unauthorized → Clear session + redirect to login
// 403 Forbidden → Redirect to access denied page
// 5xx Server Error → Retry with exponential backoff
```

---

## 🚀 Installation

### 1. Install NuGet Package

```bash
dotnet add package Linbik.YARP
```

### 2. Add Dependencies

Ensure you have:
- `Linbik.Core` (for HTTP client)
- `Linbik.JwtAuthManager` (for session management)

---

## ⚙️ Configuration

### appsettings.json

```json
{
  "Linbik": {
    "ServerUrl": "https://linbik.com",
    "ServiceId": "your-service-guid",
    "ApiKey": "linbik_abc123..."
  },
  "LinbikAuth": {
    "CookieName": ".MyBlog.Auth",
    "SessionUserKey": "_LinbikUser",
    "SessionTokensKey": "_LinbikTokens",
    "SessionRefreshTokenKey": "_LinbikRefreshToken"
  },
  "ReverseProxy": {
    "Routes": {
      "payment-route": {
        "ClusterId": "payment-cluster",
        "Match": {
          "Path": "/payment-gateway/{**catch-all}"
        },
        "Transforms": [
          {
            "PathRemovePrefix": "/payment-gateway"
          }
        ]
      },
      "courier-route": {
        "ClusterId": "courier-cluster",
        "Match": {
          "Path": "/courier-service/{**catch-all}"
        },
        "Transforms": [
          {
            "PathRemovePrefix": "/courier-service"
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

### Program.cs

```csharp
using Linbik.Core.Extensions;
using Linbik.JwtAuthManager.Extensions;
using Linbik.YARP.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Linbik.Core (HTTP client)
builder.Services.AddLinbikHttpClient(builder.Configuration);

// 2. Add Linbik.JwtAuthManager (cookie auth + session)
builder.Services.AddLinbikAuthentication(builder.Configuration);

// 3. Add Linbik.YARP (API Gateway)
builder.Services.AddLinbikYarp(builder.Configuration);

var app = builder.Build();

// 4. Use authentication
app.UseAuthentication();
app.UseAuthorization();

// 5. Use YARP reverse proxy
app.MapReverseProxy();

app.Run();
```

---

## 📖 Usage Examples

### Example 1: Payment Gateway Call

**Client code (MyBlog)**:

```csharp
[Authorize]
public class CheckoutController : Controller
{
    private readonly HttpClient _httpClient;

    public CheckoutController(IHttpClientFactory factory)
    {
        _httpClient = factory.CreateClient();
    }

    public async Task<IActionResult> Charge(decimal amount)
    {
        // YARP automatically injects Payment Gateway token
        var response = await _httpClient.PostAsJsonAsync(
            "/payment-gateway/api/charge",
            new { Amount = amount });

        if (response.IsSuccessStatusCode)
        {
            return Ok("Payment successful");
        }

        return BadRequest("Payment failed");
    }
}
```

**What happens behind the scenes**:

1. Request: `POST /payment-gateway/api/charge`
2. YARP intercepts the request
3. Retrieves `payment-gateway` token from session
4. Adds header: `Authorization: Bearer {token}`
5. Forwards to: `https://payment-gateway.com/api/charge`
6. Returns response to client

### Example 2: Multiple Service Orchestration

```csharp
public async Task<IActionResult> ProcessOrder(OrderDto order)
{
    // 1. Charge payment (via Payment Gateway)
    var paymentResponse = await _httpClient.PostAsJsonAsync(
        "/payment-gateway/api/charge",
        new { Amount = order.TotalAmount });

    if (!paymentResponse.IsSuccessStatusCode)
        return BadRequest("Payment failed");

    // 2. Create shipment (via Courier Service)
    var shipmentResponse = await _httpClient.PostAsJsonAsync(
        "/courier-service/api/shipments",
        new { OrderId = order.Id, Address = order.ShippingAddress });

    if (!shipmentResponse.IsSuccessStatusCode)
    {
        // Rollback payment if shipment fails
        await _httpClient.PostAsync("/payment-gateway/api/refund", ...);
        return BadRequest("Shipment creation failed");
    }

    return Ok("Order processed successfully");
}
```

**Benefits**:
- ✅ No manual token management
- ✅ Automatic token refresh
- ✅ Unified error handling
- ✅ Clean, readable code

---

## 🔧 Advanced Configuration

### Custom Token Provider

If you need custom token retrieval logic:

```csharp
public class CustomTokenProvider : ITokenProvider
{
    public async Task<string?> GetTokenAsync(
        HttpContext context, 
        string servicePackageName)
    {
        // Custom logic to retrieve token
        // Example: From database, cache, or external service
        var tokens = await _cache.GetAsync<List<IntegrationToken>>(
            $"user_{userId}_tokens");
        
        return tokens.FirstOrDefault(t => 
            t.ServicePackage == servicePackageName)?.Token;
    }
}

// Register in Program.cs
builder.Services.AddSingleton<ITokenProvider, CustomTokenProvider>();
```

### Circuit Breaker Pattern

```json
{
  "ReverseProxy": {
    "Clusters": {
      "payment-cluster": {
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:10",
            "Timeout": "00:00:05",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        },
        "Destinations": {
          "destination1": {
            "Address": "https://payment-gateway.com"
          }
        }
      }
    }
  }
}
```

### Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("payment-limiter", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
    });
});

// Apply to specific routes
"payment-route": {
  "ClusterId": "payment-cluster",
  "RateLimiterPolicy": "payment-limiter",
  "Match": {
    "Path": "/payment-gateway/{**catch-all}"
  }
}
```

---

## 🔒 Security Considerations

### 1. Token Storage

Tokens are stored in **session** (server-side), not cookies (client-side).

- ✅ More secure than localStorage/sessionStorage
- ✅ Tokens never exposed to client JavaScript
- ✅ Automatic cleanup on session expiration

### 2. HTTPS Only

**Always use HTTPS in production**:

```csharp
builder.Services.AddLinbikYarp(options =>
{
    options.RequireHttps = true; // Enforces HTTPS
});
```

### 3. IP Whitelisting

Integration services can whitelist your gateway IP:

```csharp
// In Linbik.App service configuration
AllowedIPs = "203.0.113.10,203.0.113.11"
```

### 4. Request Validation

Add custom validation middleware:

```csharp
app.Use(async (context, next) =>
{
    // Validate request origin, headers, etc.
    if (!IsValidRequest(context))
    {
        context.Response.StatusCode = 403;
        return;
    }
    
    await next();
});
```

---

## 🐛 Troubleshooting

### Issue: "Token not found in session"

**Cause**: User hasn't authenticated via Linbik yet.

**Solution**:
```csharp
// Ensure authentication flow completed
if (!User.Identity.IsAuthenticated)
{
    return RedirectToAction("Login", "Account");
}
```

### Issue: "401 Unauthorized from integration service"

**Cause**: Token expired or invalid.

**Solution**:
- YARP should auto-refresh tokens
- Check `RefreshToken` in session
- Verify integration service's public key matches

### Issue: "Route not found"

**Cause**: YARP route configuration mismatch.

**Solution**:
```bash
# Check route configuration
dotnet run --urls "https://localhost:5001" --verbose

# Test route manually
curl https://localhost:5001/payment-gateway/api/health
```

### Debug Mode

Enable detailed logging:

```json
{
  "Logging": {
    "LogLevel": {
      "Yarp": "Debug",
      "Linbik.YARP": "Debug"
    }
  }
}
```

---

## 📊 Architecture Diagrams

### Token Injection Flow

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ POST /payment-gateway/api/charge
       ↓
┌─────────────┐
│    YARP     │
│   Gateway   │
└──────┬──────┘
       │ 1. Intercept request
       │ 2. Get session tokens
       │ 3. Find "payment-gateway" token
       │ 4. Add Authorization header
       ↓
┌─────────────┐
│  Payment    │
│  Gateway    │
└─────────────┘
```

### Token Refresh Flow

```
┌─────────────┐
│    YARP     │
└──────┬──────┘
       │ Token expired?
       ↓ YES
┌─────────────┐
│ Linbik.Core │ ← RefreshTokensAsync(refreshToken)
│ HTTP Client │
└──────┬──────┘
       │ POST /oauth/refresh
       ↓
┌─────────────┐
│ Linbik.App  │
└──────┬──────┘
       │ New tokens
       ↓
┌─────────────┐
│   Session   │ ← Update tokens
│   Storage   │
└─────────────┘
```

---

## 🔄 Comparison: With vs Without YARP

### Without YARP (Manual Token Management)

```csharp
public class CheckoutController : Controller
{
    private readonly IHttpClientFactory _factory;

    public async Task<IActionResult> Charge(decimal amount)
    {
        // 1. Get tokens from session (manual)
        var tokensJson = HttpContext.Session.GetString("_LinbikTokens");
        var tokens = JsonSerializer.Deserialize<List<IntegrationToken>>(tokensJson);
        
        // 2. Find specific service token (manual)
        var paymentToken = tokens.FirstOrDefault(t => 
            t.ServicePackage == "payment-gateway");
        
        if (paymentToken == null)
            return Unauthorized("Payment service token not found");
        
        // 3. Check expiration (manual)
        if (paymentToken.ExpiresAt < DateTime.UtcNow)
        {
            // 4. Refresh token (manual)
            var refreshToken = HttpContext.Session.GetString("_LinbikRefreshToken");
            // ... refresh logic ...
        }
        
        // 5. Make request with token (manual)
        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", paymentToken.Token);
        
        var response = await httpClient.PostAsJsonAsync(
            "https://payment-gateway.com/api/charge",
            new { Amount = amount });
        
        return Ok();
    }
}
```

### With YARP (Automatic)

```csharp
public class CheckoutController : Controller
{
    private readonly HttpClient _httpClient;

    public async Task<IActionResult> Charge(decimal amount)
    {
        // All token management handled by YARP!
        var response = await _httpClient.PostAsJsonAsync(
            "/payment-gateway/api/charge",
            new { Amount = amount });
        
        return Ok();
    }
}
```

**Code reduction**: ~30 lines → 5 lines (83% less code!)

---

## 📚 API Reference

### ITokenProvider

```csharp
public interface ITokenProvider
{
    Task<string?> GetTokenAsync(HttpContext context, string servicePackageName);
    Task RefreshTokenAsync(HttpContext context, string servicePackageName);
}
```

### YARPOptions

```csharp
public class YARPOptions
{
    public bool RequireHttps { get; set; } = true;
    public int TokenRefreshThresholdMinutes { get; set; } = 5;
    public bool EnableCircuitBreaker { get; set; } = true;
    public bool EnableRetryPolicy { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
}
```

---

## 🎯 Best Practices

### 1. Use Named HttpClient

```csharp
// Register named client
builder.Services.AddHttpClient("LinbikGateway", client =>
{
    client.BaseAddress = new Uri("https://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Use in controller
private readonly HttpClient _httpClient;

public MyController(IHttpClientFactory factory)
{
    _httpClient = factory.CreateClient("LinbikGateway");
}
```

### 2. Handle Errors Gracefully

```csharp
try
{
    var response = await _httpClient.PostAsync(...);
    response.EnsureSuccessStatusCode();
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Payment gateway request failed");
    return StatusCode(503, "Payment service unavailable");
}
```

### 3. Cache Token Metadata

```csharp
// Cache service base URLs to avoid session reads
private static readonly Dictionary<string, string> ServiceUrls = new()
{
    ["payment-gateway"] = "https://payment-gateway.com",
    ["courier-service"] = "https://courier-service.com"
};
```

---

## 🔗 Related Libraries

- **Linbik.Core**: HTTP client for Linbik.App communication
- **Linbik.JwtAuthManager**: Cookie authentication + session management
- **Linbik.Server**: JWT validation for integration services

---

## 📄 License

This project is licensed under a proprietary license. See LICENSE file for details.

---

## 📞 Support

**Issues**: [GitHub Issues](https://github.com/tepecam18/Linbik/issues)  
**Documentation**: [Linbik.App README](../../../Linbik.App/README.md)

---

**Linbik.YARP** - _Simplify your integration service communication_ 🚀
