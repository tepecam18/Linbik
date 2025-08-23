# Linbik Kütüphaneleri Kullanım Kılavuzu - AspNet Projesi

Bu doküman, AspNet projesinde Linbik kütüphanelerinin nasıl kullanılacağını açıklar.

## 📋 İçindekiler

- [Proje Yapısı](#proje-yapısı)
- [Kurulum](#kurulum)
- [Konfigürasyon](#konfigürasyon)
- [Servis Ekleme](#servis-ekleme)
- [Kimlik Doğrulama](#kimlik-doğrulama)
- [Yetkilendirme](#yetkilendirme)
- [Middleware Kullanımı](#middleware-kullanımı)
- [API Endpoints](#api-endpoints)
- [Örnekler](#örnekler)

## 🏗️ Proje Yapısı

AspNet projesi aşağıdaki Linbik kütüphanelerini kullanır:

- **Linbik.Core**: Temel servisler ve middleware
- **Linbik.JwtAuthManager**: JWT tabanlı kimlik doğrulama
- **Linbik.Server**: Sunucu tarafı uygulama kimlik doğrulaması
- **Linbik.YARP**: Reverse proxy ve yönlendirme

## 📦 Kurulum

### 1. Proje Referansları

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\src\AspNet\Linbik.JwtAuthManager\Linbik.JwtAuthManager.csproj" />
  <ProjectReference Include="..\..\..\src\AspNet\Linbik.Server\Linbik.Server.csproj" />
  <ProjectReference Include="..\..\..\src\AspNet\Linbik.YARP\Linbik.YARP.csproj" />
</ItemGroup>
```

### 2. Using Direktifleri

```csharp
using Linbik.Core.Extensions;
using Linbik.JwtAuthManager.Extensions;
using Linbik.Server.Extensions;
using Linbik.Server.Interfaces;
using Linbik.YARP.Extensions;
```

## ⚙️ Konfigürasyon

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

## 🔧 Servis Ekleme

### 1. Temel Linbik Servisleri

```csharp
// Program.cs - Servis Ekleme Bölümü
builder.Services
    .AddLinbik() // Temel Linbik servisleri
    .AddJwtAuth(true) // JWT kimlik doğrulama (PKCE etkin)
    .AddLinbikServer() // Sunucu servisleri
    .AddProxy(); // Proxy servisleri
```

### 2. Özel Konfigürasyon ile Servis Ekleme

```csharp
// Alternatif: Özel konfigürasyon ile
builder.Services.AddLinbik(conf =>
{
    conf.AppIds = new string[] { "app1", "app2" };
    conf.AllowAllApp = false;
    conf.Version = "dev2025";
});
```

### 3. Repository Ekleme

```csharp
// Repository servisini ekle
builder.Services.AddSingleton<ILinbikServerRepository, LinbikServerRepository>();
```

## 🔐 Kimlik Doğrulama

### 1. Authentication Şemaları Ekleme

```csharp
// Program.cs - Authentication Bölümü
builder.Services
    .AddAuthentication()
    .AddLinbikScheme(builder.Configuration) // Linbik kullanıcı şeması
    .AddLinbikAppScheme(builder.Configuration); // Linbik uygulama şeması
```

### 2. Authentication Şemaları Açıklaması

- **LinbikScheme**: Kullanıcı kimlik doğrulaması için
- **LinbikAppScheme**: Uygulama kimlik doğrulaması için

## 🛡️ Yetkilendirme

### 1. Authorization Politikaları

```csharp
// Program.cs - Authorization Bölümü
builder.Services.AddAuthorization(options =>
{
    // YARP Linbik uygulamaları için politika
    options.AddPolicy("LinbikAppProxyPolicy", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddAuthenticationSchemes("LinbikAppScheme");
    });
});
```

### 2. Politika Kullanımı

```csharp
// Controller'da kullanım
[Authorize(Policy = "LinbikAppProxyPolicy")]
public class SecureController : ControllerBase
{
    // Güvenli endpoint'ler
}
```

## 🔄 Middleware Kullanımı

### 1. Middleware Sıralaması

```csharp
// Program.cs - Middleware Bölümü
var app = builder.Build();

// Middleware sırası önemli!
app.UseRouting();
app.UseAuthentication(); // Önce kimlik doğrulama
app.UseAuthorization();  // Sonra yetkilendirme

// Linbik middleware'leri
app.UseLinbikServer(); // Linbik sunucu endpoint'leri
app.UseJwtAuth();      // JWT kimlik doğrulama
app.UseProxy();        // YARP proxy
```

### 2. Middleware Açıklamaları

- **UseLinbikServer()**: `/linbik/app-login` gibi sunucu endpoint'lerini etkinleştirir
- **UseJwtAuth()**: JWT kimlik doğrulama endpoint'lerini etkinleştirir
- **UseProxy()**: YARP reverse proxy'i etkinleştirir

## 🌐 API Endpoints

### JWT Kimlik Doğrulama Endpoint'leri

- `POST /linbik/login` - Kullanıcı girişi
- `POST /linbik/refresh-token` - Token yenileme
- `POST /linbik/logout` - Çıkış
- `GET /linbik/pkce-start` - PKCE başlatma (etkinse)

### Sunucu Kimlik Doğrulama Endpoint'leri

- `POST /linbik/app-login` - Uygulama girişi

### YARP Proxy Endpoint'leri

- `/{prefixPath}/*` - Yapılandırılan proxy rotaları
  - `/webhook/*` → Webhook servisleri
  - `/app/*` → Uygulama servisleri

## 📝 Örnekler

### 1. Controller Örneği

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
        return Ok($"Merhaba {user}!");
    }
}
```

### 2. Repository Örneği

```csharp
// Repositories/LinbikServerRepository.cs
public class LinbikServerRepository : ILinbikServerRepository
{
    public async Task<bool> ValidateAppAsync(string appId, string token)
    {
        // Uygulama doğrulama mantığı
        // Burada veritabanı kontrolü yapılabilir
        return true;
    }
}
```

### 3. Özel Middleware Ekleme

```csharp
// Program.cs'de ek middleware ekleme
app.UseMiddleware<UnauthorizedPayloadMiddleware>();
```

## 🔒 Güvenlik

### 1. Anahtar Güvenliği

- **PrivateKey**: En az 64 karakter uzunluğunda güçlü anahtarlar kullanın
- **AppIds**: Sadece gerekli uygulama ID'lerini ekleyin
- **AllowAllApp**: Production'da `false` olarak ayarlayın

### 2. Token Yönetimi

- Access token'lar kısa süreli (varsayılan 15 dakika)
- Refresh token'lar uzun süreli (varsayılan 15 gün)
- PKCE desteği ile güvenli kod değişimi

### 3. CORS ve Referer Kontrolü

```csharp
// JWT konfigürasyonunda
"JwtAuth": {
  "RefererControl": true,
  "Routes": {
    "allowed-domain": "https://yourdomain.com"
  }
}
```

## 🚨 Hata Ayıklama

### 1. Log Seviyeleri

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

### 2. Yaygın Hatalar ve Çözümleri

1. **Configuration Binding Hatası**
   - Property isimlerinin doğru olduğundan emin olun
   - PascalCase kullanın (AllowAllApp, AppIds, PrivateKey)

2. **Authentication Scheme Hatası**
   - Şemaların doğru sırada eklendiğini kontrol edin
   - UseAuthentication() önce, UseAuthorization() sonra

3. **Private Key Hatası**
   - Anahtarların yeterli uzunlukta olduğunu doğrulayın
   - En az 64 karakter olmalı

4. **Middleware Sırası Hatası**
   - UseRouting() → UseAuthentication() → UseAuthorization() → Linbik Middleware'leri

## 🔧 Gelişmiş Konfigürasyon

### 1. PKCE Ayarları

```csharp
// PKCE etkinleştirme/devre dışı bırakma
builder.Services.AddJwtAuth(true);  // PKCE etkin
builder.Services.AddJwtAuth(false); // PKCE devre dışı
```

### 2. Token Süreleri

```json
{
  "Linbik": {
    "JwtAuth": {
      "AccessTokenExpiration": 15,    // 15 dakika
      "RefreshTokenExpiration": 15    // 15 gün
    },
    "Server": {
      "AccessTokenExpiration": 60     // 60 dakika
    }
  }
}
```

### 3. YARP Proxy Ayarları

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

## 📚 Ek Kaynaklar

- [Linbik.Core Documentation](../Linbik.Core/README.md)
- [Linbik.JwtAuthManager Documentation](../Linbik.JwtAuthManager/README.md)
- [Linbik.Server Documentation](../Linbik.Server/README.md)
- [Linbik.YARP Documentation](../Linbik.YARP/README.md)

## 🤝 Destek

Sorunlarınız için:
1. GitHub Issues kullanın
2. Bu dokümantasyonu kontrol edin
3. Log dosyalarını inceleyin
4. Middleware sırasını kontrol edin

---

**Not**: Bu doküman AspNet projesinde Linbik kütüphanelerinin kullanımını kapsar. Gelişmiş özellikler için ilgili proje README dosyalarını inceleyin.
