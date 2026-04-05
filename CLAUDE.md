# CLAUDE.md — Linbik Framework

## Project Overview

Linbik is an open-source **OAuth 2.1 Authentication Framework** for .NET 10.0. It provides cookie-based JWT authentication, S2S token operations, YARP reverse proxy integration, and a CLI tool. The project is in **preview** status (v1.2.0-preview.1).

- **GitHub**: https://github.com/tepecam18/Linbik
- **License**: MIT
- **Author**: tepecam18

## Repository Structure

```
src/AspNet/
├── Linbik.Core/            # Core library: shared models, config, health checks, HTTP resilience
├── Linbik.JwtAuthManager/  # Cookie-based JWT auth (login/logout/refresh endpoints, RSA-256)
├── Linbik.Server/          # Integration service JWT validation (dual-scheme: User + S2S)
├── Linbik.YARP/            # YARP reverse proxy with automatic token injection
└── Linbik.CLI/             # CLI tool (`linbik init|status|export-config`)

examples/
├── AspNet/AspNet/          # ASP.NET Core MVC example (all 4 libraries)
└── nuxt/                   # Nuxt 3 example (client-side OAuth 2.1 + PKCE)
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10.0 (net10.0) |
| Language | C# with nullable reference types |
| Auth | OAuth 2.1 Authorization Code Flow + PKCE, RSA-256 JWT |
| HTTP Resilience | Microsoft.Extensions.Http.Resilience (Polly) |
| Reverse Proxy | YARP 2.3.0 |
| CLI | System.CommandLine 2.0.0-beta4 |
| Frontend Example | Nuxt 3, Vue 3, TypeScript |
| Versioning | GitVersion (Mainline mode, `v` prefix) |
| Containers | Docker (docker-compose.yml) |

## Build Commands

```bash
# Build individual projects
dotnet build src/AspNet/Linbik.Core/Linbik.Core.csproj
dotnet build src/AspNet/Linbik.JwtAuthManager/Linbik.JwtAuthManager.csproj
dotnet build src/AspNet/Linbik.Server/Linbik.Server.csproj
dotnet build src/AspNet/Linbik.YARP/Linbik.YARP.csproj
dotnet build src/AspNet/Linbik.CLI/Linbik.CLI.csproj

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
UseHttpsRedirection → UseAuthentication → UseAuthorization → UseLinbikRateLimiting → UseLinbikJwtAuth → MapLinbikIntegrationEndpoints → UseLinbikYarp
```

### Naming Conventions
- **Attributes**: `Linbik*Attribute` (e.g., `LinbikAuthorizeAttribute`, `LinbikS2SAuthorizeAttribute`)
- **Extensions**: `Linbik*Extensions` static classes
- **Interfaces**: `I` prefix (e.g., `IS2STokenProvider`, `IS2SServiceClient`)
- **Options**: `*Options` + `*OptionsValidator` pairs
- **Responses**: `LBaseResponse<T>` standardized wrapper

### Authentication Schemes
- `LinbikUserService` — User context (cookie-forwarded JWT)
- `LinbikS2S` — Machine-to-machine (S2S token)

### Project Dependencies
```
Linbik.Core ← Linbik.JwtAuthManager
Linbik.Core ← Linbik.Server
Linbik.Core ← Linbik.YARP
```

### Key Endpoints (JwtAuthManager)
- `POST /linbik/login` — OAuth flow initiation
- `POST /linbik/logout` — Session termination
- `POST /linbik/refresh` — Token refresh

### Rate Limiting Policies
- `LinbikAuth` — Standard auth rate limit
- `LinbikAuthStrict` — Strict auth rate limit

## Code Style

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled
- File-scoped namespaces
- Options pattern with validation
- Each library has its own `.sln` file (no monolithic solution)
- NuGet packages built with `GeneratePackageOnBuild`

## Important Notes

- Startup validation via `app.EnsureLinbik()` — fails fast on misconfiguration
- Keyless Mode allows dev provisioning without pre-registration
- Health check integration via `Microsoft.Extensions.Diagnostics.HealthChecks`
- OpenTelemetry tracing in Linbik.Server
