# Linbik AspNet.Examples

ASP.NET Core MVC uygulaması ile Linbik Authentication Framework entegrasyonu örneği.

## 🎯 Genel Bakış

Bu proje, Linbik kütüphanelerinin tam entegrasyonunu gösteren bir demo uygulamasıdır:

- **Linbik.Core** - OAuth 2.1 Authorization Code Flow client
- **Linbik.JwtAuthManager** - JWT authentication ve cookie yönetimi
- **Linbik.Server** - Integration service JWT doğrulama
- **Linbik.YARP** - Reverse proxy ile otomatik token injection

## 📦 Proje Yapısı

```
AspNet.Examples/
├── Controllers/
│   ├── TestController.cs        ← Dashboard ve test endpoint'leri
│   └── IntegrationController.cs ← Integration service demo
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

// ✅ Linbik - Fluent builder pattern for all services
builder.Services.AddLinbik()
    .AddLinbikJwtAuth()
    .AddLinbikServer()
    .AddLinbikYarp();

// ✅ Linbik Integration Handler
builder.Services.AddLinbikIntegrationHandler();

// ✅ Linbik Rate Limiting
builder.Services.AddLinbikRateLimiting();

var app = builder.Build();

// ✅ Validate all registered Linbik modules at startup
app.EnsureLinbik();

// Middleware pipeline
app.UseRouting();
app.UseLinbikRateLimiting();
app.UseAuthentication();
app.UseAuthorization();

// Map Linbik OAuth endpoints
app.UseLinbikJwtAuth();

// Map integration proxy endpoints
app.UseLinbikYarp();

app.Run();
```

## 📚 Endpoint'ler

### Dashboard (MVC)

| URL | Açıklama |
|-----|----------|
| `/Test` | Ana dashboard sayfası |
| `/Test/Index` | Kullanıcı durumu ve token bilgileri |

### OAuth Endpoints (UseLinbikJwtAuth)

| URL | Method | Açıklama |
|-----|--------|----------|
| `/linbik/login` | GET | Linbik'e yönlendir ve authorization code al |
| `/linbik/logout` | POST | Cookie'leri temizle ve çıkış yap |
| `/linbik/refresh` | POST | Refresh token ile yeni token'lar al |

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
| `/api/integration/protected` | GET | ✅ User JWT | `[LinbikUserServiceAuthorize]` | Protected endpoint |
| `/api/integration/user-profile` | GET | ✅ User JWT | `[LinbikUserServiceAuthorize]` | Kullanıcı profili |
| `/api/integration/process` | POST | ✅ User JWT | `[LinbikUserServiceAuthorize]` | İşlem yap |
| `/api/integration/user-data` | GET | ✅ User JWT | `[LinbikUserServiceAuthorize]` | Kullanıcı verileri |
| `/api/integration/s2s/sync` | POST | ✅ S2S JWT | `[LinbikS2SAuthorize]` | S2S senkronizasyon |
| `/api/integration/s2s/health` | GET | ✅ S2S JWT | `[LinbikS2SAuthorize]` | S2S sağlık |
| `/api/integration/s2s/webhook/{eventType}` | POST | ✅ S2S JWT | `[LinbikS2SAuthorize("Service")]` | S2S webhook (servis) |
| `/api/integration/s2s/batch` | POST | ✅ S2S JWT | `[LinbikS2SAuthorize]` | S2S toplu işlem |
| `/api/integration/s2s/platform-event` | POST | ✅ S2S JWT | `[LinbikS2SAuthorize("Linbik")]` | Platform olayı |

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

### 2. [LinbikUserServiceAuthorize] Attribute

Integration service endpoint'lerini (user context ile) korumak için:

```csharp
[LinbikUserServiceAuthorize]
[HttpGet("protected")]
public IActionResult Protected()
{
    var claims = HttpContext.GetLinbikClaims();
    return Ok(claims);
}
```

### 3. [LinbikS2SAuthorize] Attribute

Service-to-service endpoint'lerini (kullanıcı bağlamı olmadan) korumak için:

```csharp
// Herhangi bir S2S token kabul eder
[LinbikS2SAuthorize]
[HttpPost("s2s/sync")]
public IActionResult S2SSync() { ... }

// Sadece servis S2S token'ları (role=Service)
[LinbikS2SAuthorize("Service")]
[HttpPost("s2s/webhook/{eventType}")]
public IActionResult S2SWebhook(string eventType) { ... }

// Sadece platform token'ları (role=Linbik)
[LinbikS2SAuthorize("Linbik")]
[HttpPost("s2s/platform-event")]
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

# S2S endpoint (S2S JWT gerekir, token_type=s2s)
curl https://localhost:7020/api/integration/s2s/sync \
  -H "Authorization: Bearer eyJ..."

# S2S platform event (role=Linbik gerekir)
curl https://localhost:7020/api/integration/s2s/platform-event \
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
**Last Updated**: 2 Nisan 2026
