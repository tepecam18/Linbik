# 🔐 Linbik Framework

**Federated Identity Platform with OAuth 2.1 Authorization Code Flow**

Linbik is a complete OAuth 2.1 authentication framework for .NET that enables multi-service federated identity management with per-service JWT tokens.

> ⚠️ **Preview**: Bu kütüphaneler aktif geliştirme aşamasındadır. Deneysel amaçla kullanılabilir.

---

## 🎯 What is Linbik?

Linbik connects distributed services through a single verified identity. It's **NOT a data warehouse** - it's an authentication layer that lets services communicate securely using a shared identity reference.

### Core Concept

```
User → Authenticates once on linbik.com (cookie, 7 days)
     → Service requests authorization via SDK
     → Linbik issues authorization code (5 min, single-use)
     → Service exchanges code for tokens (API Key auth)
     → Service gets: user profile + integration service JWTs + refresh token
     → Service manages own session
```

### Problem and Solution

#### ❌ Traditional Approach

```
User → Separate account for each service
     → Separate password for each service
     → Duplicate data entry (address, payment)
     → Data inconsistency
```

#### ✅ Linbik Approach

```
User → Authenticate once on Linbik
     → All services recognize same identity
     → Data stays in each service's database
     → Services communicate via identity reference
```

---

## 📦 NuGet Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| **Linbik.Core** | Core interfaces, models, configuration and auth client | [![NuGet](https://img.shields.io/nuget/v/Linbik.Core)](https://www.nuget.org/packages/Linbik.Core) |
| **Linbik.JwtAuthManager** | Cookie-based JWT auth, login/logout endpoints, rate limiting | [![NuGet](https://img.shields.io/nuget/v/Linbik.JwtAuthManager)](https://www.nuget.org/packages/Linbik.JwtAuthManager) |
| **Linbik.PasetoAuthManager** | Cookie-based PASETO v4.public (Ed25519) auth, same endpoint shape as JwtAuthManager | [![NuGet](https://img.shields.io/nuget/v/Linbik.PasetoAuthManager)](https://www.nuget.org/packages/Linbik.PasetoAuthManager) |
| **Linbik.Server** | Integration service token validation, Application auth, telemetry | [![NuGet](https://img.shields.io/nuget/v/Linbik.Server)](https://www.nuget.org/packages/Linbik.Server) |
| **Linbik.YARP** | YARP reverse proxy token injection, Application client | [![NuGet](https://img.shields.io/nuget/v/Linbik.YARP)](https://www.nuget.org/packages/Linbik.YARP) |
| **Linbik.Slices** | Vertical-slice mediator (source-generated dispatch, `ILinbikValidator`) | [![NuGet](https://img.shields.io/nuget/v/Linbik.Slices)](https://www.nuget.org/packages/Linbik.Slices) |
| **Linbik.Slices.Generators** | Roslyn incremental source generator + analyzer for Linbik.Slices | [![NuGet](https://img.shields.io/nuget/v/Linbik.Slices.Generators)](https://www.nuget.org/packages/Linbik.Slices.Generators) |
| **Linbik.Cli** | CLI tool for project setup and service management | [![NuGet](https://img.shields.io/nuget/v/Linbik.Cli)](https://www.nuget.org/packages/Linbik.Cli) |

Also available: a native **Android** client (`src/Android/Linbik.PasetoAuthManager.Android`) for the PASETO mobile-client flow — see its [README](src/Android/Linbik.PasetoAuthManager.Android/README.md).

---

## 🚀 Quick Start

### 1. Install Packages

```bash
# Core + JWT Auth (client app minimum)
dotnet add package Linbik.JwtAuthManager

# For integration services (receives JWT tokens)
dotnet add package Linbik.Server

# For YARP reverse proxy (optional)
dotnet add package Linbik.YARP

# CLI tool (optional)
dotnet tool install --global Linbik.Cli
```

### 2. Configure Your Application

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Linbik services (fluent builder pattern)
builder.Services.AddLinbik(builder.Configuration.GetSection("Linbik"))
    .AddLinbikJwtAuth()       // Cookie-based login/logout/refresh endpoints
    .AddLinbikServer()        // Integration service JWT validation (optional)
    .AddLinbikYarp();         // YARP proxy with token injection (optional)

var app = builder.Build();

// Validate all Linbik modules at startup
app.EnsureLinbik();

app.UseAuthentication();
app.UseAuthorization();

// Map OAuth endpoints: /linbik/login, /linbik/logout, /linbik/refresh
app.UseLinbikJwtAuth();

// Map integration proxy: /{packageName}/{**path} (optional)
app.UseLinbikYarp();

app.Run();
```

### 3. Configure appsettings.json

```json
{
  "Linbik": {
    "LinbikUrl": "https://api.linbik.com",
    "ServiceId": "your-service-guid",
    "ApiKey": "lnbk_your_api_key",
    "Clients": [
      {
        "ClientId": "your-client-guid",
        "RedirectUrl": "https://yourapp.com",
        "ActionResultType": "Redirect"
      }
    ]
  }
}
```

### 4. Register Your Service

Register your service at [linbik.com](https://linbik.com) to get your ServiceId, ClientId, and ApiKey. Alternatively, use **Keyless Mode** for zero-configuration development (enabled by default).

---

## 🏗️ Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│              Linbik Platform (linbik.com)                    │
│  - User authentication (cookie-based)                       │
│  - Authorization code generation                            │
│  - Token exchange (api.linbik.com)                          │
│  - Service registration & management                        │
│  - User consent management                                  │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  │ OAuth 2.1 Flow
                  │
┌─────────────────▼───────────────────────────────────────────┐
│                   Client Application                        │
│                  (Your Service)                              │
│  - Uses Linbik.Core, Linbik.JwtAuthManager                  │
│  - Redirects user to linbik.com for auth                    │
│  - Exchanges code for tokens (API Key)                      │
│  - Stores tokens in cookies                                 │
│  - Calls integration services with JWTs                     │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  │ JWT Authentication / Application
                  │
┌─────────────────▼───────────────────────────────────────────┐
│              Integration Services                           │
│        (Payment Gateway, Courier, etc.)                     │
│  - Uses Linbik.Server for JWT validation                    │
│  - Validates JWT with RSA public key                        │
│  - Extracts userId from token claims                        │
│  - Performs requested operation                             │
└─────────────────────────────────────────────────────────────┘
```

### OAuth 2.1 Flow

```
┌─────────┐                                    ┌────────────┐
│  User   │                                    │   MyApp    │
└────┬────┘                                    └─────┬──────┘
     │                                               │
     │  1. Click "Login with Linbik"                 │
     │◄──────────────────────────────────────────────┤
     │                                               │
     │  2. SDK → POST api.linbik.com/api/oauth/initiate
     │     (ApiKey + ClientId + CodeChallenge)        │
     │                                               │
     │  3. Redirect to linbik.com/auth/{token}       │
     ├──────────────────────────────────────────────►│
     │                                               │
┌────▼──────┐                                        │
│  Linbik   │                                        │
└────┬──────┘                                        │
     │  4. Login + Consent Screen                    │
     │  5. Generate Authorization Code               │
     │  6. Redirect with code                        │
     ├──────────────────────────────────────────────►│
     │                                               │
     │  7. POST api.linbik.com/api/oauth/token       │
     │     (ApiKey + Code)                           │
     │                                               │
     │  8. Return:                                   │
     │     - User profile (userId, username, etc.)   │
     │     - Integration tokens (JWT per service)    │
     │     - Refresh token (30 days)                 │
     ├──────────────────────────────────────────────►│
     │                                               │
     │  9. Call integration service                  │
     │     Authorization: Bearer {jwt}               │
     ├──────────────────────────────────────────────►│
```

---

## ✨ Key Features

### 1. OAuth 2.1 Authorization Code Flow
- ✅ **Initiate + Consent flow** via `POST /api/oauth/initiate`
- ✅ **PKCE support** (RFC 7636) for public clients
- ✅ **Short-lived codes** (5 minutes, single-use)
- ✅ **Cookie-based user auth** (7-day sessions)
- ✅ **API Key authentication** for token exchange

### 2. Multi-Service Integration
- ✅ **Per-service JWT tokens** signed with individual RSA keys
- ✅ **User consent management** - Users choose which services to grant access
- ✅ **Service relationships** - Main services integrate with specialized services
- ✅ **Automatic token injection** via YARP integration

### 3. Application (Service-to-Service) Communication
- ✅ **IApplicationTokenProvider** for Application token management
- ✅ **IApplicationServiceClient** typed HTTP client with automatic token injection
- ✅ **Config-based** (package name) and **dynamic** (service ID) targets
- ✅ **Role-based Application authorization** (`[LinbikApplicationAuthorize("Service")]` / `[LinbikApplicationAuthorize("Linbik")]`)

### 4. Keyless Mode (Zero-Config Development)
- ✅ **Auto-provisioning** - SDK creates temporary service on first run
- ✅ **Auto-claim** - First login claims ownership
- ✅ **Credential caching** in `.linbik/credentials.json`

### 5. Security Features
- ✅ **RSA-256 JWT signing** (asymmetric crypto, 2048-bit keys)
- ✅ **PKCE** (Proof Key for Code Exchange)
- ✅ **IP Whitelisting** (CIDR notation support)
- ✅ **Rate limiting** with configurable policies
- ✅ **Hashed API keys** in database
- ✅ **Cross-scheme injection protection** (user vs Application tokens)
- ✅ **HttpOnly secure session cookies**

---

## 📚 Documentation

### Package Documentation

- [**Linbik.Core**](src/AspNet/Linbik.Core/README.md) - Core interfaces, models, and configuration
- [**Linbik.JwtAuthManager**](src/AspNet/Linbik.JwtAuthManager/README.md) - JWT auth, login endpoints, rate limiting
- [**Linbik.PasetoAuthManager**](src/AspNet/Linbik.PasetoAuthManager/README.md) - PASETO v4.public auth, login endpoints, rate limiting
- [**Linbik.Server**](src/AspNet/Linbik.Server/README.md) - Integration service token validation and Application auth
- [**Linbik.YARP**](src/AspNet/Linbik.YARP/README.md) - YARP token provider and Application client
- [**Linbik.Slices**](src/AspNet/Linbik.Slices/README.md) - Vertical-slice mediator with source-generated dispatch
- [**Linbik.CLI**](src/AspNet/Linbik.CLI/README.md) - Command-line tool
- [**Android Client**](src/Android/Linbik.PasetoAuthManager.Android/README.md) - Native Kotlin client for the PASETO mobile flow

### Examples

- [**AspNet.Examples**](examples/AspNet/AspNet/README.md) - Complete OAuth client + integration service demo (PASETO-based)
- [**Nuxt.Examples**](examples/nuxt/README.md) - Nuxt 3 frontend integration (the production client is Nuxt 4 — see roadmap in `PROJECT_STATUS.md`)

### Guides

- [**Migration Guide**](MIGRATION_GUIDE.md) - Migrate from v1.x to v2.0+

---

## 💡 Usage Scenarios

### Scenario 1: E-Commerce Ecosystem

```csharp
// 1. User clicks "Buy" on ShopX → Linbik authenticates
// 2. ShopX exchanges code for tokens
var tokenResponse = await authService.ExchangeCodeForTokensAsync(authCode);

// 3. ShopX calls PaymentPro with per-service JWT
var paymentToken = tokenResponse.Integrations
    .First(i => i.PackageName == "paymentpro").Token;

await httpClient.PostAsync("https://paymentpro.com/charge", new
{
    linbik_user_id = tokenResponse.UserId,
    amount = 15000
}, headers: new { Authorization = $"Bearer {paymentToken}" });

// 4. PaymentPro validates the PASETO token with its public key via [LinbikDelegatedAuthorize]
// 5. Looks up user's saved card by linbik_user_id → Processes payment
```

### Scenario 2: Application Communication (Webhooks/Callbacks)

```csharp
// Service A → Service B (no user context)
var result = await _applicationClient.PostByIdAsync<PaymentNotification, NotifyResponse>(
    merchantServiceId,             // dynamic target
    "/api/webhooks/payment",
    new PaymentNotification { OrderId = "123", Status = "completed" }
);
```

### Scenario 3: YARP Proxy (Automatic Token Injection)

```csharp
// Client request → /payment-gateway/api/charge
// YARP reads integration cookie, injects Authorization header, proxies request
// No manual token management needed!
app.UseLinbikYarp();
```

---

## 🛠️ Development

### Build All Projects

```bash
dotnet build
```

### Run Example Application

```bash
cd examples/AspNet/AspNet
dotnet run
```

### CLI Tool

```bash
# Initialize a new Linbik project
linbik init

# Check service status
linbik status

# Export configuration
linbik export-config

# Diagnose Program.cs/appsettings/credential mismatches (read-only)
linbik doctor
```

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.

> ⚠️ **Preview**: This framework is under active development. APIs may change.

**Version**: 1.2.0  
**Platform**: ASP.NET Core 10.0 (net10.0)  
**Contact**: info@linbik.com  
**Repository**: https://github.com/tepecam18/Linbik

---

**Linbik** - _One identity, infinite connections_ 🌐

---

**Last Updated**: 9 Eylül 2026

## Development checks

The repository-wide .NET solution is `Linbik.slnx`. Run `dotnet build Linbik.slnx -c Release`
and `dotnet test tests/Linbik.Tests/Linbik.Tests.csproj -c Release --no-build`.
See [clean code changes and verification](docs/CLEAN_CODE.md) for the Nuxt and Android checks,
behavior changes, and validation limits. The quality workflow runs these checks on pull requests.
