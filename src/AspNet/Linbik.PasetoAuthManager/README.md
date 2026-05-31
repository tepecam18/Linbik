# Linbik.PasetoAuthManager

Provides seamless Linbik PASETO Authorize and Login capabilities. Uses PASETO v4.public (Ed25519) for cookie-based local authentication. Registration and login flows are executed on Linbik.com and handled securely by this package.

## Installation

```bash
dotnet add package Linbik.PasetoAuthManager
```

## Usage

```csharp
builder.Services.AddLinbik(config)
    .AddLinbikPasetoAuth(config);
```

## License

MIT
