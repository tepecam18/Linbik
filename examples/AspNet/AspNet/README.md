# Linbik AspNet.Examples

ASP.NET Core MVC uygulaması ile Linbik Authentication Framework entegrasyonu örneği.

## 🎯 Genel Bakış

Bu proje, Linbik kütüphanelerinin tam entegrasyonunu gösteren bir demo uygulamasıdır:

- **Linbik.Core** - OAuth 2.1 Authorization Code Flow client
- **Linbik.PasetoAuthManager** - PASETO (v4.public) authentication ve cookie yönetimi
- **Linbik.YARP** - Reverse proxy ile otomatik token injection (user-context + application)
- **Linbik.Server** - Integration service PASETO doğrulama — bu örnekte `Program.cs`'te **devre dışı** (`.AddLinbikServer()` satırı yorumda), bkz. aşağıdaki "Program.cs Yapılandırması"

## 📦 Proje Yapısı

```
AspNet.Examples/
├── Controllers/
│   ├── TestController.cs             ← Dashboard ve test endpoint'leri
│   ├── IntegrationController.cs      ← Integration service demo
│   └── ApplicationDemoController.cs  ← Application client demo (IApplicationServiceClient)
├── Models/
│   └── DashboardViewModel.cs    ← View model
├── Views/
│   └── Test/
│       └── Index.cshtml         ← Dashboard view
├── Program.cs                   ← Uygulama bootstrap
├── appsettings.json             ← Yapılandırma
└── README.md                    ← Bu dosya
```

## 🚀 Hızlı Başlangıç

### 1. Gereksinimleri Kontrol Et

- .NET 10.0 SDK
- Linbik platformu çalışır durumda ([linbik.com](https://linbik.com) veya lokal geliştirme)

### 2. Yapılandırmayı Güncelle

`appsettings.json` dosyasını düzenle:

```json
{
  "Linbik": {
    "LinbikUrl": "https://api.linbik.com",
    "ServiceId": "YOUR-SERVICE-GUID",
    "Clients": [
      {
        "ClientId": "YOUR-CLIENT-GUID",
        "BaseUrl": "https://localhost:7020",
        "ClientType": "Web"
      }
    ],
    "ApiKey": "lnbk_YOUR_API_KEY"
  }
}
```

### 3. Uygulamayı Çalıştır

```bash
cd examples/AspNet/AspNet
dotnet run
```

Uygulama https://localhost:7020 adresinde başlayacak.

## 🔧 Kütüphane Entegrasyonu

### Program.cs Yapılandırması

```csharp
var builder = WebApplication.CreateBuilder(args);

// MVC Services
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

// ✅ Linbik - Fluent builder pattern for all Linbik services
builder.Services.AddLinbik(builder.Configuration.GetSection("Linbik"))
    .AddLinbikPasetoAuth()
    //.AddLinbikServer(); // şu an devre dışı — bu örnek Linbik.Server entegrasyonunu (dual PASETO
                           // scheme + delegated/application endpoint doğrulaması) göstermiyor
    .AddLinbikYarp();

// ✅ Linbik Integration Handler
builder.Services.AddLinbikIntegrationHandler();

// ✅ Linbik Rate Limiting
builder.Services.AddLinbikRateLimiting();

var app = builder.Build();

// ✅ Validate all registered Linbik modules at startup
app.EnsureLinbik();

app.MapOpenApi();

// Middleware pipeline
app.UseRouting();
app.UseLinbikRateLimiting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Test}/{action=Index}/{id?}");

// ✅ Map Linbik OAuth endpoints (PASETO)
app.UseLinbikPasetoAuth();

// ✅ Map Linbik Integration webhook endpoints
app.MapLinbikIntegrationEndpoints();

// Map integration proxy endpoints
app.UseLinbikYarp();

app.Run();
```

> **Not**: `.AddLinbikServer()` çağrısı şu an yorum satırı olarak devre dışı bırakılmış. Bu örnek uygulama bu haliyle `Linbik.Server`'ın sunduğu dual-scheme (delegated/application) PASETO doğrulamasını göstermez — `IntegrationController`'daki `[LinbikDelegatedAuthorize]`/`[LinbikApplicationAuthorize]` örnekleri yalnızca referans amaçlıdır. `Linbik.Server` kullanımı için bkz. [Linbik.Server README](../../../src/AspNet/Linbik.Server/README.md).

## 📚 Endpoint'ler

### Dashboard (MVC)

| URL | Açıklama |
|-----|----------|
| `/Test` | Ana dashboard sayfası |
| `/Test/Index` | Kullanıcı durumu ve token bilgileri |

### OAuth Endpoints (UseLinbikPasetoAuth)

| URL | Method | Açıklama |
|-----|--------|----------|
| `/api/Linbik/login` | GET | Linbik'e yönlendir ve authorization code al |
| `/api/Linbik/callback` | GET | Authorization code'u token ile değiştir |
| `/api/Linbik/logout` | GET | Cookie'leri temizle ve çıkış yap |
| `/api/Linbik/refresh` | POST | Refresh token ile yeni token'lar al |

### Test Endpoints

| URL | Method | Açıklama |
|-----|--------|----------|
| `/Test/Protected` | GET | [LinbikAuthorize] korumalı endpoint |
| `/Test/Profile` | GET | JWT claims ile kullanıcı profili |
| `/Test/RefreshTest` | POST | Refresh token testi |
| `/Test/TestRateLimit` | GET | Rate limiting testi |
| `/Test/TestStrictRateLimit` | GET | Strict rate limiting testi |
| `/Test/Metrics` | GET | Linbik metrikleri |
| `/Test/SecurityInfo` | GET | Güvenlik yapılandırması |

### Integration Service Demo

| URL | Method | Auth | Attribute | Açıklama |
|-----|--------|------|-----------|----------|
| `/api/integration/health` | GET | ❌ | — | Sağlık kontrolü |
| `/api/integration/info` | GET | ❌ | — | Servis bilgisi |
| `/api/integration/public-data` | GET | ❌ | — | Public veri |
| `/api/integration/echo` | POST | ❌ | — | Echo endpoint |
| `/api/integration/protected` | GET | ✅ User JWT | `[LinbikDelegatedAuthorize]` | Protected endpoint |
| `/api/integration/user-profile` | GET | ✅ User JWT | `[LinbikDelegatedAuthorize]` | Kullanıcı profili |
| `/api/integration/process` | POST | ✅ User JWT | `[LinbikDelegatedAuthorize]` | İşlem yap |
| `/api/integration/user-data` | GET | ✅ User JWT | `[LinbikDelegatedAuthorize]` | Kullanıcı verileri |
| `/api/integration/application/sync` | POST | ✅ Application JWT | `[LinbikApplicationAuthorize]` | Application senkronizasyon |
| `/api/integration/application/health` | GET | ✅ Application JWT | `[LinbikApplicationAuthorize]` | Application sağlık |
| `/api/integration/application/webhook/{eventType}` | POST | ✅ Application JWT | `[LinbikApplicationAuthorize("Service")]` | Application webhook (servis) |
| `/api/integration/application/batch` | POST | ✅ Application JWT | `[LinbikApplicationAuthorize]` | Application toplu işlem |
| `/api/integration/application/platform-event` | POST | ✅ Application JWT | `[LinbikApplicationAuthorize("Linbik")]` | Platform olayı |

### Application Service Demo (Application Client)

`ApplicationDemoController`, `IApplicationServiceClient` kullanarak başka bir entegrasyon servisine (config-based veya dinamik service ID ile) nasıl istek atılacağını gösterir:

| URL | Method | Açıklama |
|-----|--------|----------|
| `/api/application-demo/call-by-package/{packageName}` | GET | Config'deki paket adına göre servise istek at |
| `/api/application-demo/sync-to/{packageName}` | POST | Servise veri senkronize et |
| `/api/application-demo/webhook-to/{packageName}/{eventType}` | POST | Servise webhook bildirimi gönder |
| `/api/application-demo/call-by-id/{serviceId}` | GET | Servis ID'sine göre dinamik istek at (config gerekmez) |
| `/api/application-demo/callback-to/{serviceId}` | POST | Servis ID'sine göre dinamik callback gönder |
| `/api/application-demo/error-demo/{packageName}` | GET | `LBaseResponse` hata yönetimi demosu |

### YARP Proxy Endpoints

| URL Pattern | Hedef |
|-------------|-------|
| `/api/payment/**` | → payment-gateway servisi |
| `/api/survey/**` | → survey-service servisi |
| `/api/serverTest/**` | → service-test (localhost) |

## 🔐 Authentication Özellikleri

### 1. [LinbikAuthorize] Attribute

Controller action'larını korumak için:

```csharp
[LinbikAuthorize]
[HttpGet]
public IActionResult Protected()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Ok(new { userId });
}
```

### 2. [LinbikDelegatedAuthorize] Attribute

Integration service endpoint'lerini (user context ile) korumak için:

```csharp
[LinbikDelegatedAuthorize]
[HttpGet("protected")]
public IActionResult Protected()
{
    // PasetoBearerHandler ClaimsPrincipal'ı token'ın ham claim anahtarlarıyla
    // (ClaimTypes.* eşlemesi olmadan) oluşturur — bkz. Linbik.Server README
    var userId = User.FindFirst("sub")?.Value;
    var userName = User.FindFirst("preferred_username")?.Value;
    return Ok(new { userId, userName });
}
```

### 3. [LinbikApplicationAuthorize] Attribute

Service-to-service endpoint'lerini (kullanıcı bağlamı olmadan) korumak için:

```csharp
// Herhangi bir Application token kabul eder
[LinbikApplicationAuthorize]
[HttpPost("application/sync")]
public IActionResult ApplicationSync() { ... }

// Sadece servis Application token'ları (role=Service)
[LinbikApplicationAuthorize("Service")]
[HttpPost("application/webhook/{eventType}")]
public IActionResult ApplicationWebhook(string eventType) { ... }

// Sadece platform token'ları (role=Linbik)
[LinbikApplicationAuthorize("Linbik")]
[HttpPost("application/platform-event")]
public IActionResult OnPlatformEvent() { ... }
```

### 4. Rate Limiting

```csharp
[EnableRateLimiting("LinbikAuth")]
public IActionResult RateLimitedAction()
{
    return Ok();
}
```

## ⚙️ Yapılandırma Seçenekleri

### appsettings.json

```json
{
  "Linbik": {
    // Core ayarları
    "LinbikUrl": "https://api.linbik.com",  // veya lokal: "http://localhost:5481"
    "Name": "Web App",
    "ServiceId": "guid",
    "ApiKey": "lnbk_xxx",
    
    // Client yapılandırması
    "Clients": [
      {
        "ClientId": "your-client-guid",
        "RedirectUrl": "https://yourapp.com",
        "ActionResultType": "Redirect"
      }
    ],
    
    // JwtAuth ayarları
    "JwtAuth": {
      "SecretKey": "min-32-chars-secret-key",
      "JwtIssuer": "linbik-example",
      "JwtAudience": "linbik-example-client",
      "PkceEnabled": false,
      "AutoUpdateRedirectUri": true
    },
    
    // Server ayarları (Integration service)
    "Server": {
      "PublicKey": "MIIBIjAN...",
      "PackageName": "service-test"
    },
    
    // Resilience ayarları (Polly)
    "Resilience": {
      "Enabled": true,
      "MaxRetryAttempts": 3,
      "RetryDelayMs": 1000,
      "CircuitBreakerEnabled": true,
      "TimeoutSeconds": 30
    },
    
    // Rate Limit ayarları
    "RateLimit": {
      "Enabled": true,
      "PolicyName": "LinbikAuth",
      "PermitLimit": 10,
      "WindowSeconds": 60,
      "QueueLimit": 0
    },
    
    // Audit ayarları
    "Audit": {
      "Enabled": true,
      "LogSuccessfulOperations": true,
      "IncludeIpAddress": true,
      "MaskSensitiveData": true
    },

    // Heartbeat (SDK-to-server sağlık sinyali)
    "EnableHeartbeat": true,
    "HeartbeatIntervalSeconds": 60,
    
    // YARP ayarları
    "YARP": {
      "IntegrationServices": {
        "payment-gateway": {
          "SourcePath": "/api/payment",
          "TargetBaseUrl": "https://payment.example.com",
          "TargetPath": "/api/v1/pay"
        }
      }
    }
  }
}
```

## 🧪 Test Senaryoları

### Senaryo 1: Basic Login Flow

1. https://localhost:7020/Test adresini aç
2. "Linbik ile Giriş Yap" butonuna tıkla
3. Linbik'te giriş yap
4. Dashboard'a geri dön ve kullanıcı bilgilerini gör

### Senaryo 2: Protected Endpoint

```bash
# Giriş yapmadan (401 döner)
curl https://localhost:7020/Test/Protected

# Giriş yaptıktan sonra (200 döner)
curl https://localhost:7020/Test/Protected \
  -H "Cookie: authToken=eyJ..."
```

### Senaryo 3: Integration Service Test

```bash
# Public endpoint (auth gerekmez)
curl https://localhost:7020/api/integration/health

# User-context protected endpoint (JWT gerekir)
curl https://localhost:7020/api/integration/protected \
  -H "Authorization: Bearer eyJ..."

# Application endpoint (Application JWT gerekir, token_type=apps)
curl https://localhost:7020/api/integration/application/sync \
  -H "Authorization: Bearer eyJ..."

# Application platform event (role=Linbik gerekir)
curl https://localhost:7020/api/integration/application/platform-event \
  -H "Authorization: Bearer eyJ..."
```

### Senaryo 4: YARP Proxy Test

```bash
# Payment gateway'e proxy (otomatik token injection)
curl https://localhost:7020/api/payment/charge \
  -H "Cookie: integration_payment-gateway=eyJ..."
```

## 🔍 Troubleshooting

### "Authorization code is missing"

**Çözüm**: linbik.com'da doğru redirect URL'yi kontrol et.

### "Token exchange failed"

**Çözüm**: 
1. `appsettings.json`'daki ApiKey doğru mu?
2. Linbik (api.linbik.com) çalışıyor mu?
3. ServiceId ve ClientId doğru mu?

### "Invalid JWT signature"

**Çözüm**: Server.PublicKey değerinin doğru olduğunu kontrol et.

### "Rate limit exceeded"

**Çözüm**: Rate limit ayarlarını kontrol et veya bekleme süresi dolduktan sonra tekrar dene.

## 📖 İlgili Dokümantasyon

- [Linbik.Core README](../../../src/AspNet/Linbik.Core/README.md)
- [Linbik.JwtAuthManager README](../../../src/AspNet/Linbik.JwtAuthManager/README.md)
- [Linbik.Server README](../../../src/AspNet/Linbik.Server/README.md)
- [Linbik.YARP README](../../../src/AspNet/Linbik.YARP/README.md)
- [Linbik Platform](https://linbik.com) — Servis kayıt ve yönetim

## 📄 Lisans

Bu proje özel bir lisans altında yayınlanmaktadır.

---

**Version**: 1.2.0  
**Last Updated**: 9 Eylül 2026
