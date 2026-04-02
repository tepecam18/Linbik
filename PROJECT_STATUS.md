# 📊 Linbik Framework - Project Status

**Last Updated**: 2 Nisan 2026  
**Version**: 1.2.0-preview.1  
**Status**: 🟡 Preview — Active Development

---

## 🎯 Project Overview

Linbik Framework, OAuth 2.1 Authorization Code Flow ile çoklu servis federated identity yönetimi sağlayan açık kaynak bir .NET kütüphane ailesidir.

**Platform**: ASP.NET Core 10.0 (net10.0)  
**NuGet**: [nuget.org/profiles/tepecam18](https://www.nuget.org/profiles/tepecam18)

---

## 📦 Package Summary

| Package | Version | Status | Description |
|---------|---------|--------|-------------|
| **Linbik.Core** | 1.2.0-preview.1 | 🟡 Preview | Core interfaces, models, configuration |
| **Linbik.JwtAuthManager** | 1.2.0-preview.1 | 🟡 Preview | Cookie JWT auth, login endpoints, rate limiting |
| **Linbik.Server** | 1.2.0-preview.1 | 🟡 Preview | Integration JWT validation, S2S auth, telemetry |
| **Linbik.YARP** | 1.2.0-preview.1 | 🟡 Preview | YARP proxy, S2S client |
| **Linbik.CLI** | 0.0.1 | 🔴 Alpha | CLI tool (init, status, export-config) |

---

## ✅ Implemented Features

### Core Platform
- ✅ OAuth 2.1 Authorization Code Flow with PKCE
- ✅ Initiate → Consent → Token Exchange flow
- ✅ Multi-service JWT token generation (per-service RSA keys)
- ✅ Refresh token management (30 days default)
- ✅ Keyless Mode (zero-config development)
- ✅ Multi-client support (Web, Mobile, Admin)
- ✅ Heartbeat (SDK-to-server health signals)

### Security
- ✅ RSA-256 asymmetric JWT signing (2048-bit keys)
- ✅ Hashed API keys (SHA256 via ServiceApiKey.KeyHash)
- ✅ Short-lived authorization codes (5 min, single-use)
- ✅ PKCE for public clients
- ✅ IP Whitelisting (CIDR notation)
- ✅ Rate limiting (LinbikAuth + LinbikAuthStrict policies)
- ✅ Cross-scheme injection protection (user vs S2S)
- ✅ HttpOnly secure session cookies

### Integration Services
- ✅ Dual JWT auth schemes (LinbikUserService + LinbikS2S)
- ✅ [LinbikUserServiceAuthorize] attribute
- ✅ [LinbikS2SAuthorize] with role-based access (Service | Linbik)
- ✅ ILinbikIntegrationHandler lifecycle events
- ✅ OpenTelemetry integration
- ✅ Health checks

### S2S Communication
- ✅ IS2STokenProvider (config-based + dynamic targets)
- ✅ IS2SServiceClient typed HTTP client (GET/POST/PUT/DELETE/PATCH)
- ✅ Auto token caching and refresh
- ✅ LBaseResponse<T> enforcement

### YARP Proxy
- ✅ Cookie-based token injection
- ✅ Per-service routing (/{packageName}/{**path})
- ✅ AddLinbikTokenTransform for custom YARP routes

---

## ⚠️ Known Limitations

- Unit test coverage yok (manuel test ile doğrulanıyor)
- CLI tool erken alpha aşamasında
- JavaScript/TypeScript SDK henüz yok (Nuxt örneği manuel entegrasyon)
- OpenAPI/Swagger spec henüz oluşturulmadı

---

## 🚀 Roadmap

### Planned
- [ ] Unit test suite
- [ ] `@linbik/nuxt` — Nuxt module
- [ ] `@linbik/node` — Node.js SDK
- [ ] OpenAPI spec generation
- [ ] Video tutorials

---

## 📞 Support & Contact

**Email**: info@linbik.com  
**Repository**: https://github.com/tepecam18/Linbik  
**Issues**: https://github.com/tepecam18/Linbik/issues

---

**Version**: 1.2.0-preview.1  
**Last Updated**: 2 Nisan 2026
