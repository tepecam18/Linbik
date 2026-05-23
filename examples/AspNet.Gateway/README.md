# Linbik Gateway — AspNet.Gateway Örneği

.NET 10 ile YARP tabanlı API gateway. Auth gateway'de yapılır, downstream
servisler `[Authorize]` çalıştırmaz; yalnızca `X-Linbik-*` header'larını okur.

Tüm tokenlar **PASETO v4.public (Ed25519)** kullanır.
> `Linbik.PasetoAuthManager` paketi hazır olduğunda `GatewayAuthenticationExtensions.cs`
> tek satır swap ile güncellenir.

## Yapı

```
AspNet.Gateway/
  Linbik.Gateway.Sample.sln           — tüm projeleri kapsar
  docker-compose.yml                  — tüm servisleri tek komutta ayağa kaldır
  .dockerignore

  Linbik.Gateway.Sample/              — gateway (host: 5100, container: 8080)
    Gateway/                          — PASETO auth + ClaimToHeaderTransform
    OpenApi/                          — N:N OpenAPI birleştirici
    appsettings.{Development,Docker}.json
    Dockerfile

  Downstream.Sample/                  — echo servisi (5101)
    Controllers/EchoController.cs
    Dockerfile

  Downstream.Sample2/                 — products servisi (5102) — N:N OpenAPI demo
    Controllers/ProductsController.cs
    Dockerfile

  tools/
    Linbik.Gateway.TokenIssuer/       — PASETO dev token üretici CLI

  tests/
    Linbik.Gateway.Sample.Tests/      — xUnit + WebApplicationFactory

  DESIGN.md
  README.md
```

## Hızlı Başlangıç

### Seçenek A — Docker Compose (önerilen)

```powershell
# Linbik kökünden:
docker compose -f examples/AspNet.Gateway/docker-compose.yml up --build
```

Servisler:
- **Gateway**:      http://localhost:5100
- **Health**:       http://localhost:5100/health
- **Self OpenAPI**: http://localhost:5100/openapi/self.json (dev'de erişime açık)
- **Delegated OpenAPI**: http://localhost:5100/openapi/delegated.json
- **Apps OpenAPI**: http://localhost:5100/openapi/apps.json
- **Scalar UI**:    http://localhost:5100/scalar/self  (diğer scheme'ler de)

Downstream'ler doğrudan erişilemez (sadece iç ağda).

### Seçenek B — dotnet run (3 terminal)

```powershell
# Terminal 1
cd Downstream.Sample;  dotnet run

# Terminal 2
cd Downstream.Sample2; dotnet run

# Terminal 3
cd Linbik.Gateway.Sample; dotnet run
```

### Seçenek C — Solution build & test

```powershell
cd examples/AspNet.Gateway
dotnet build Linbik.Gateway.Sample.sln
dotnet test  tests/Linbik.Gateway.Sample.Tests
```

## PASETO Token Üretme (dev)

Dev ortamında `KeylessMode: true` ile token imzası doğrulanmaz; yine de
*geçerli formatlı* bir PASETO gerekir. Üretmek için:

```powershell
cd examples/AspNet.Gateway/tools/Linbik.Gateway.TokenIssuer

# 1. Anahtar çifti üret (sadece bir kez)
dotnet run -- keygen

# 2. Token üret
dotnet run -- issue --scheme self --private-key <PRIV> `
                    --sub user-42 --claim role=admin --claim email=u@x.io
```

Çıktıyı `Authorization: Bearer <token>` veya cookie olarak geçirin.

> Dev'de `KeylessMode: true` (appsettings.Development.json + docker-compose env)
> ayarlıdır; gerçek PASETO anahtarı olmadan başlatılabilir.

## Manuel Test

### Self — cookie ile (KeylessMode)

```powershell
# KeylessMode'da geçerli bir PASETO üretmek gerekiyor.
# PasetoHelperService.GenerateKeyPair() ile önce anahtar çifti üretin,
# ardından CreateTokenAsync() ile token oluşturun.
# Geliştirme kolaylığı için imza doğrulaması KeylessMode=true ile atlanır.

curl http://localhost:5100/self/echo `
  -H "Cookie: authToken=<paseto-token>"
```

### Delegated — Bearer token ile

```powershell
curl http://localhost:5100/delegated/echo `
  -H "Authorization: Bearer <paseto-delegated-token>"
```

### Apps — Bearer token ile (S2S)

```powershell
curl http://localhost:5100/apps/echo `
  -H "Authorization: Bearer <paseto-apps-token>"
```

### Spoof testi — X-Linbik-Sub manipülasyonu engellenir

```powershell
# Dışarıdan X-Linbik-Sub gönderilse bile gateway bunu siler ve
# JWT'deki gerçek sub değerini yazar.
curl http://localhost:5100/self/echo `
  -H "Cookie: authToken=<token>" `
  -H "X-Linbik-Sub: attacker"
# Yanıtta X-Linbik-Sub, token'daki gerçek değeri gösterir.
```

## OpenAPI Dokümanları

| URL | Ortam |
|-----|-------|
| `GET /openapi/self.json`      | Sadece Development (prod'da 404) |
| `GET /openapi/delegated.json` | Development + Production |
| `GET /openapi/apps.json`      | Development + Production |

Scalar UI: `http://localhost:5100/scalar/v1`

### Prod'da Self dokümanı açmak için

`appsettings.Production.json`:
```json
{
  "LinbikGateway": {
    "OpenApi": {
      "Self": { "ExposeInProduction": true }
    }
  }
}
```

## Üretim Yapılandırması

`appsettings.json` → `LinbikGateway:Auth` bölümüne public key'leri ekle:

```json
"LinbikGateway": {
  "Auth": {
    "Self": {
      "PublicKey": "<Base64-32-byte-Ed25519-public-key>",
      "Audience": "linbik-client"
    },
    "Delegated": {
      "PublicKey": "<Linbik.Api-public-key>",
      "Audience": "linbik-delegated"
    },
    "Apps": {
      "PublicKey": "<Linbik.Api-public-key>",
      "Audience": "linbik-apps"
    }
  }
}
```

Ed25519 anahtar çifti üretmek için:
```csharp
var helper = new PasetoHelperService();
var (priv, pub) = helper.GenerateKeyPair();
// pub → appsettings PublicKey
// priv → token issuer'ın güvenli ortam değişkeni
```

## Güvenlik Notu (Üretim)

Gateway'in claim injection güvenliği **ağ izolasyonuna** dayanır. Downstream
servisler sadece gateway'den erişilebilir olmalı (private VNet, internal LB,
K8s NetworkPolicy, mTLS). Aksi halde bir saldırgan downstream'i doğrudan
çağırıp `X-Linbik-Sub` header'ını spoof edebilir.

İleriki sürüm `Linbik.Gateway` paketinde HMAC imzalı internal token katmanı
(`X-Linbik-Sig`) planlanmaktadır.

## Downstream Servis Entegrasyonu

Downstream servis auth kullanmaz. Header'ları şöyle okur:

```csharp
var userId   = Request.Headers["X-Linbik-Sub"].ToString();
var username = Request.Headers["X-Linbik-Username"].ToString();
var scheme   = Request.Headers["X-Linbik-Scheme"].ToString(); // self|delegated|apps
```

Kendi OpenAPI dokümanında `x-linbik-audiences` extension'ı ile hangi gateway
dokümanına gireceğini belirtir:

```csharp
operation.Extensions["x-linbik-audiences"] = new OpenApiArray
{
    new OpenApiString("self"),
    new OpenApiString("delegated")
};
```

Belirtilmezse cluster metadata'sındaki `linbik:audiences` değeri kullanılır.
