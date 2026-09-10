# 📊 Linbik Framework - Project Status

**Last Updated**: 10 Eylül 2026
**Version**: 1.2.7  
**Status**: 🔵 Beta — Active Development

---

## 🎯 Project Overview

Linbik Framework, OAuth 2.1 Authorization Code Flow ile çoklu servis federated identity yönetimi sağlayan açık kaynak bir .NET kütüphane ailesidir.

**Platform**: ASP.NET Core 10.0 (net10.0)  
**NuGet**: [nuget.org/profiles/tepecam18](https://www.nuget.org/profiles/tepecam18)

---

## 📦 Package Summary

| Package | Version | Status | Description |
|---------|---------|--------|-------------|
| **Linbik.Core** | 1.2.7 | 🔵 Beta | Core interfaces, models, configuration |
| **Linbik.JwtAuthManager** | 1.2.7 | 🔵 Beta | Cookie JWT auth, login endpoints, rate limiting |
| **Linbik.PasetoAuthManager** | 1.2.7 | 🔵 Beta | Cookie PASETO v4.public (Ed25519) auth, same endpoint shape as JWT |
| **Linbik.Server** | 1.2.7 | 🔵 Beta | Integration token validation, Application auth, telemetry |
| **Linbik.YARP** | 1.2.7 | 🔵 Beta | YARP proxy, Application client |
| **Linbik.Slices** | 1.2.7 | 🔵 Beta | Vertical-slice mediator (source-generated endpoint registration) |
| **Linbik.Slices.Generators** | 1.2.7 | 🔵 Beta | Roslyn incremental source generator + analyzer (LINBIK001-004) |
| **Linbik.CLI** | 1.2.7 | 🔵 Beta | CLI tool (init, status, export-config, doctor) |
| **Linbik.PasetoAuthManager.Android** | 1.2.4 | 🔵 Beta | Native Kotlin client for the PASETO mobile-client flow |

---

## ✅ Implemented Features

### Core Platform
- ✅ OAuth 2.1 Authorization Code Flow with PKCE
- ✅ Initiate → Consent → Token Exchange flow
- ✅ Multi-service token generation (per-service RSA keys for JWT, Ed25519 for PASETO)
- ✅ Refresh token management (30 days default)
- ✅ Keyless Mode (zero-config development)
- ✅ Multi-client support (Web, Mobile, Admin)
- ✅ Heartbeat (SDK-to-server health signals)

### Auth Managers
- ✅ **Linbik.JwtAuthManager** — RS256/HS256 JWT, cookie-based login/callback/logout/refresh
- ✅ **Linbik.PasetoAuthManager** — PASETO v4.public (Ed25519), identical endpoint shape to JWT
- ✅ Rate limiting registration is shared in Linbik.Core

### Security
- ✅ RSA-256 asymmetric JWT signing (2048-bit keys) / Ed25519 PASETO signing
- ✅ Hashed API keys (SHA256 via ServiceApiKey.KeyHash)
- ✅ Short-lived authorization codes (5 min, single-use)
- ✅ PKCE for public clients
- ✅ IP Whitelisting (CIDR notation)
- ✅ Rate limiting (`LinbikGeneral` fixed-window + `LinbikStrict` token-bucket policies)
- ✅ Cross-scheme injection protection (Self vs Delegated vs Application)
- ✅ HttpOnly secure session cookies
- ✅ JWT/PASETO error responses are shared and covered by callback regression tests

### Integration Services
- ✅ Three auth schemes: `LinbikScheme` (client-side), `LinbikDelegated` and `LinbikApplication` (server-side, PASETO)
- ✅ `[LinbikAuthorize]` attribute
- ✅ `[LinbikApplicationAuthorize]` with role-based access (Service | Linbik)
- ✅ ILinbikIntegrationHandler lifecycle events
- ✅ OpenTelemetry integration
- ✅ Health checks

### Application Communication
- ✅ IApplicationTokenProvider (config-based + dynamic targets)
- ✅ IApplicationServiceClient typed HTTP client (GET/POST/PUT/DELETE/PATCH)
- ✅ Auto token caching and refresh
- ✅ LBaseResponse<T> enforcement
- ✅ Rename from "S2S" to "Application" terminology is complete in code (config keys, comments, logs)

### YARP Proxy
- ✅ Cookie-based token injection
- ✅ Per-service routing (/{packageName}/{**path})
- ✅ AddLinbikTokenTransform for custom YARP routes

### Vertical-Slice Mediator (Linbik.Slices)
- ✅ Source-generated dispatch (no reflection) via `Linbik.Slices.Generators`
- ✅ `ILinbikValidator` pipeline validation
- ✅ Roslyn analyzer with 4 diagnostics (LINBIK001-004), including deny-by-default flow authorization checks

### Android Client (Linbik.PasetoAuthManager.Android)
- ✅ Native Kotlin library (`linbikauth`) for the PASETO mobile-client (`ActionResultType: "Json"`) flow
- ✅ RFC 8252-compliant Custom Tabs + deep-link callback handling
- ✅ Persistent cross-request cookie jar (`LinbikSharedCookieJar`) bridging OkHttp and `android.webkit.CookieManager`
- ✅ Sample app published on Google Play as a closed test (join `https://groups.google.com/g/linbik`, then install via `https://play.google.com/store/apps/details?id=com.linbik`)
- ✅ Android build/unit tests are defined in the quality workflow; Maven/JitPack publish identity remains a separate task

### Identity Model (staged)
- 🧪 `LActor` (`Linbik.Core/Identity/LActor.cs`) — actor-aware model unifying user/service identity for validators; claim/flow casing is now covered by regression tests

---

## ⚠️ Known Limitations

- Automated .NET, Nuxt and Android regression suites exist; production OAuth and device lifecycle coverage remains limited. See [verification scope](docs/CLEAN_CODE.md).
- CLI tool erken alpha aşamasında
- JavaScript/TypeScript SDK henüz yok (Nuxt örneği manuel entegrasyon)
- OpenAPI/Swagger spec henüz oluşturulmadı
- `Linbik.Core` şu an hem abstraction/config hem de somut SDK implementasyonunu (`LinbikAuthService` gibi) aynı pakette barındırıyor — ayrıştırma planlanıyor
- Android istemcisi henüz CI'a bağlı değil; Maven/JitPack yayın kimliği netleşmedi
- Gateway header sanitization is enabled; downstream deployment must still enforce its gateway trust boundary.

### Son düzenlemede giderilen sorunlar

- JWT `ReturnAuthError` uygulaması ortak Core yardımcısına taşındı ve callback hata testleri eklendi.
- JWT/PASETO rate limiting kayıtları mevcut ortak Core uzantısını kullanıyor.
- `LActor` claim ve flow casing uyumsuzluğu giderildi; testlerle doğrulandı.
- Gateway header sanitization etkinleştirildi; middleware davranışı test edildi.

Tam değişiklik listesi ve doğrulama sınırları: [Temiz kod dönüşümü](docs/CLEAN_CODE.md).

---

## 🚀 Roadmap

### Now / Known Issues
Fix the live bugs listed above, each already file-referenced there:
- [x] Implement `ReturnAuthError` in `Linbik.JwtAuthManager` (mirror the PASETO implementation)
- [x] Fix `Linbik.JwtAuthManager`'s `AddLinbikRateLimiting()` to actually call `AddCommonLinbikRateLimiting`
- [x] Deduplicate `AddLinbikRateLimiting` between JwtAuthManager/PasetoAuthManager (shared extension in Linbik.Core, or namespaced names)
- [x] Fix `LActor.FromGatewayContext` claim-key casing to match `LGatewayAuthContext.Claims`
- [x] Re-enable and verify `LinbikHeaderSanitizationMiddleware` in `examples/AspNet.Gateway`, or correct `ARCHITECTURE.md`'s claim until it's re-enabled

### Near-term
- [ ] Bring `Linbik.PasetoAuthManager/README.md` to parity with `Linbik.JwtAuthManager/README.md`
- [ ] Document `linbik doctor` and other CLI gaps
- [x] Wire the Android module into CI (workflow added; remote execution pending)
- [ ] Reconcile Android Maven/JitPack publish identity
- [ ] `@linbik/nuxt` — Nuxt module
- [ ] `@linbik/node` — Node.js SDK

### Mid-term
- [ ] Extend the repository regression suite to cover more authentication, concurrency and device scenarios
- [ ] Split `Linbik.Core` into an abstractions/config package vs a concrete client/SDK package
- [ ] Add CI smoke tests that actually run the example projects
- [ ] OpenAPI spec generation
- [ ] Video tutorials

### Blocked / Cross-repo
- [ ] **Bridge `ptts/Linbik.Api` onto the real `Linbik.PasetoAuthManager` library** — it currently has its own parallel PASETO reimplementation and doesn't consume any `Linbik.*` package. This is a hard prerequisite for writing meaningful "how to use Linbik" docs + GEO content inside `Linbik.App` (explicitly deferred, tracked separately, not part of this repo's work).
- [ ] **Rotate plaintext production secrets in `ptts/Linbik.Api/appsettings.json`** (DB password now pointing at a production host, Resend API key, PASETO private key) — external, urgent, read-only from this repo's perspective; see `ANALIZ_RAPORU.md` §6.1.

---

## 📞 Support & Contact

**Email**: info@linbik.com  
**Repository**: https://github.com/tepecam18/Linbik  
**Issues**: https://github.com/tepecam18/Linbik/issues

---

**Version**: 1.2.7  
**Last Updated**: 10 Eylül 2026
