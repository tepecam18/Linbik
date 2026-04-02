# Linbik Migration Guide

## v1.x → v2.0+ (OAuth 2.1 Upgrade)

### Breaking Changes

1. **JWT Signing Algorithm**: HS256 (symmetric) → RS256 (asymmetric RSA)
2. **Authentication Pattern**: Direct token issuance → Authorization Code Flow
3. **Token Format**: Single token → Multi-service tokens (per-service JWT)
4. **Service Model**: Shared secret → Per-service RSA key pairs
5. **Endpoint Pattern**: Direct auth → Initiate + Consent + Token Exchange

### Migration Steps

#### 1. Update Package References

```bash
dotnet add package Linbik.Core --version 1.2.0-preview.1
dotnet add package Linbik.JwtAuthManager --version 1.2.0-preview.1
dotnet add package Linbik.Server --version 1.2.0-preview.1
dotnet add package Linbik.YARP --version 1.2.0-preview.1
```

#### 2. Update Program.cs

```csharp
// ❌ Old way (v1.x)
builder.Services.AddLinbik(options => {
    options.SecretKey = "shared-secret";
});

// ✅ New way (v2.0+)
builder.Services.AddLinbik(builder.Configuration.GetSection("Linbik"))
    .AddLinbikJwtAuth()
    .AddLinbikServer()     // optional
    .AddLinbikYarp();      // optional

var app = builder.Build();
app.EnsureLinbik();

app.UseLinbikJwtAuth();  // Maps /linbik/login, /linbik/logout, /linbik/refresh
```

#### 3. Update appsettings.json

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

#### 4. Update Token Response Handling

```csharp
// ❌ Old way (v1.x)
var token = _jwtHelper.CreateToken(claims, sharedSecret);

// ✅ New way (v2.0+)
var token = await _jwtHelper.CreateTokenAsync(
    claims,
    service.PrivateKey,
    audience: service.Id.ToString()
);
```

**Model Property Changes**:

| v1.x | v2.0+ |
|------|-------|
| `UserName` | `Username` |
| `NickName` | `DisplayName` |
| `ServicePackage` | `PackageName` |

#### 5. Update YARP Token Provider

```csharp
// ❌ Old way (v1.x)
var token = await _tokenProvider.GetTokenAsync(context);

// ✅ New way (v2.0+) - Cookie-based automatic injection
app.UseLinbikYarp();
// No manual token management needed!
```

### New Features in v2.0+

- **Keyless Mode** — `KeylessMode = true` for zero-config development
- **Multi-Client** — Web, Mobile, Admin via `Clients` list
- **S2S Communication** — `IS2SServiceClient` for service-to-service calls
- **Integration Handler** — `ILinbikIntegrationHandler` lifecycle events
- **Rate Limiting** — Built-in `LinbikAuth` and `LinbikAuthStrict` policies
- **Telemetry** — OpenTelemetry integration via `AddLinbikTelemetry()`
- **Health Checks** — `AddLinbikHealthChecks()` + `UseLinbikHealthChecks()`
- **Heartbeat** — SDK health signals to server

---

## 📖 Related Documentation

- [Main README](README.md)
- [Linbik.Core](src/AspNet/Linbik.Core/README.md)
- [Linbik.JwtAuthManager](src/AspNet/Linbik.JwtAuthManager/README.md)
- [Linbik.Server](src/AspNet/Linbik.Server/README.md)
- [Linbik.YARP](src/AspNet/Linbik.YARP/README.md)

---

**Last Updated**: 2 Nisan 2026  
**Version**: 1.2.0
