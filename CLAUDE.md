# CLAUDE.md — Linbik Framework

## Project Overview

Linbik is an open-source **OAuth 2.1 Authentication Framework** for .NET 10.0. It provides cookie-based JWT and PASETO authentication, a vertical-slice mediator (`Linbik.Slices`), Application (formerly "S2S") token operations, YARP reverse proxy integration, a CLI tool, and a native Android client. The project is in **preview** status (v1.2.0-preview.1).

- **GitHub**: https://github.com/tepecam18/Linbik
- **License**: MIT
- **Author**: tepecam18

## Repository Structure

```
src/AspNet/
├── Linbik.Core/               # Core library: shared models, config, health checks, HTTP resilience
├── Linbik.JwtAuthManager/      # Cookie-based JWT auth (login/logout/refresh endpoints, RS256/HS256)
├── Linbik.PasetoAuthManager/   # Cookie-based PASETO v4.public auth (Ed25519), same endpoint shape as JWT
├── Linbik.Server/              # Integration service token validation (dual-scheme: Delegated + Application)
├── Linbik.YARP/                # YARP reverse proxy with automatic token injection
├── Linbik.CLI/                 # CLI tool (`linbik init|status|export-config|doctor`)
├── Linbik.Slices/               # Vertical-slice mediator (ILinbikValidator, LinbikSender, LValidation)
└── Linbik.Slices.Generators/    # Roslyn incremental source generator + analyzer for Linbik.Slices (LINBIK001-004)

src/Android/
└── Linbik.PasetoAuthManager.Android/  # Native Kotlin client for the PASETO mobile-client (Json) flow
    ├── linbikauth/              # Publishable Android library (com.linbik.pasetoauth)
    └── sample/                  # Minimal example app using linbikauth

examples/
├── AspNet/AspNet/          # ASP.NET Core MVC example (PASETO-based auth flow)
├── AspNet.Gateway/         # YARP API Gateway example (header-based flow authorization)
└── nuxt/                   # Nuxt 3 example (client-side OAuth 2.1 + PKCE)
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10.0 (net10.0) |
| Language | C# with nullable reference types |
| Auth | OAuth 2.1 Authorization Code Flow + PKCE; RS256/HS256 JWT or PASETO v4.public (Ed25519) |
| HTTP Resilience | Microsoft.Extensions.Http.Resilience (Polly) |
| Reverse Proxy | YARP 2.3.0 |
| CLI | System.CommandLine 2.0.0-beta4 |
| Android Client | Kotlin, OkHttp, Custom Tabs (RFC 8252) |
| Frontend Example | Nuxt 3, Vue 3, TypeScript |
| Versioning | GitVersion (Mainline mode, `v` prefix) |
| Containers | Docker (docker-compose.yml) |

## Build Commands

```bash
# Build individual projects
dotnet build src/AspNet/Linbik.Core/Linbik.Core.csproj
dotnet build src/AspNet/Linbik.JwtAuthManager/Linbik.JwtAuthManager.csproj
dotnet build src/AspNet/Linbik.PasetoAuthManager/Linbik.PasetoAuthManager.csproj
dotnet build src/AspNet/Linbik.Server/Linbik.Server.csproj
dotnet build src/AspNet/Linbik.YARP/Linbik.YARP.csproj
dotnet build src/AspNet/Linbik.CLI/Linbik.CLI.csproj
dotnet build src/AspNet/Linbik.Slices/Linbik.Slices.csproj

# Build example
dotnet build examples/AspNet/AspNet/AspNet.csproj

# Run example API (https://localhost:7020)
dotnet run --project examples/AspNet/AspNet/AspNet.csproj

# Run Nuxt example
cd examples/nuxt && npm install && npm run dev

# Docker
docker-compose up
```

## Architecture & Conventions

### Builder Pattern (Fluent API)
All libraries register via a fluent builder chain:
```csharp
builder.Services.AddLinbik(config)
    .AddLinbikJwtAuth(config)
    .AddLinbikServer(config)
    .AddLinbikYarp(config);
```

### Configuration
- All options use `IValidateOptions<T>` pattern with dedicated `*OptionsValidator` classes
- Configuration sections: `Linbik`, `Linbik:JwtAuth`, `Linbik:Server`, `Linbik:YARP`, `Linbik:RateLimiting`

### Middleware Pipeline Order
```
UseHttpsRedirection → UseAuthentication → UseAuthorization → UseLinbikJwtAuth (or UseLinbikPasetoAuth) → MapLinbikIntegrationEndpoints → UseLinbikYarp
```
`MapLinbikIntegrationEndpoints` and `UseLinbikYarp` are endpoint-mapping calls (they register routes), not classic `IApplicationBuilder` middleware — order them after `UseAuthorization()`, not as a distinct middleware stage.

### Naming Conventions
- **Attributes**: `Linbik*Attribute` (e.g., `LinbikAuthorizeAttribute`, `LinbikApplicationAuthorizeAttribute`)
- **Extensions**: `Linbik*Extensions` static classes
- **Interfaces**: `I` prefix (e.g., `IApplicationTokenProvider`, `IApplicationServiceClient`)
- **Options**: `*Options` + `*OptionsValidator` pairs
- **Responses**: `LBaseResponse<T>` standardized wrapper

### Authentication Schemes
(defined in `Linbik.Core/LinbikDefaults.cs`)
- `LinbikScheme` — Client-side HS256/RS256 JWT scheme (cookie-based, for main services)
- `LinbikDelegated` — Server-side PASETO v4.public scheme for user-initiated (delegated) requests
- `LinbikApplication` — Server-side PASETO v4.public scheme for service-to-service ("Application") requests

### Project Dependencies
```
Linbik.Core ← Linbik.JwtAuthManager
Linbik.Core ← Linbik.PasetoAuthManager
Linbik.Core ← Linbik.Server
Linbik.Core ← Linbik.YARP
Linbik.Slices ← Linbik.Slices.Generators
```

### Key Endpoints (JwtAuthManager / PasetoAuthManager — identical shape)
- `GET /linbik/login` — OAuth flow initiation
- `GET /linbik/callback` — OAuth authorization code callback
- `GET /linbik/logout` — Session termination
- `POST /linbik/refresh` — Token refresh

### Rate Limiting Policies
(registered centrally in `Linbik.Core/Extensions/LinbikRateLimitingExtensions.cs`)
- `LinbikGeneral` — Fixed-window general rate limit
- `LinbikStrict` — Token-bucket strict rate limit, applied to login/refresh endpoints via `RequireRateLimiting("LinbikStrict")`

## Code Style

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled
- File-scoped namespaces
- Options pattern with validation
- `Linbik.slnx` builds all .NET libraries, examples and the regression test suite; per-library solutions remain available
- NuGet packages built with `GeneratePackageOnBuild`

## Important Notes

- Startup validation via `app.EnsureLinbik()` — fails fast on misconfiguration
- Keyless Mode allows dev provisioning without pre-registration
- Health check integration via `Microsoft.Extensions.Diagnostics.HealthChecks`
- OpenTelemetry tracing in Linbik.Server
- `Linbik.Core` currently mixes abstractions/config with concrete SDK implementation (e.g. `LinbikAuthService`); splitting these into separate packages is a tracked roadmap item, see `PROJECT_STATUS.md`

## Repository verification

See `docs/CLEAN_CODE.md` for build/test commands and behavior changes. Regression tests live in `tests/Linbik.Tests`, `examples/nuxt/test`, and the Android library test source set.
