using Linbik.Core.Services.Interfaces;
using Paseto;
using Paseto.Builder;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Linbik.Core.Services;

/// <summary>
/// <see cref="IPasetoHelper"/> gerçekleştirimi: PASETO v4.public (Ed25519) üretimi ve doğrulaması.
/// Stateless'tir; DI'da singleton olarak kayıt edilebilir.
/// </summary>
public sealed class PasetoHelperService : IPasetoHelper
{
    /// <inheritdoc/>
    public async Task<string?> GetValidationFailureAsync(string token, string key, string audience, string issuer, PasetoMode mode)
    {
        if (string.IsNullOrWhiteSpace(audience))
            return "PASETO audience is not configured. Set Linbik:PackageName for this service.";
        if (string.IsNullOrWhiteSpace(issuer))
            return "PASETO issuer is not configured. Check Linbik:JwtIssuer.";
        if (string.IsNullOrWhiteSpace(key))
            return "PASETO validation key is missing. Check key configuration or provisioning credentials.";
        byte[] keyBytes;
        try { keyBytes = Convert.FromBase64String(key); }
        catch (FormatException) { return "PASETO validation key is not valid Base64. Check the configured key or credentials cache."; }
        if (keyBytes.Length != 32)
            return "PASETO validation key must contain 32 bytes. Check the configured key or credentials cache.";
        var prefix = mode == PasetoMode.Local ? "v4.local." : "v4.public.";
        if (!token.StartsWith(prefix, StringComparison.Ordinal))
            return "PASETO token version or purpose is incorrect for this authentication scheme.";
        var valid = mode == PasetoMode.Local
            ? await ValidateLocalTokenAsync(token, key, audience, issuer)
            : await ValidateTokenAsync(token, key, audience, issuer);
        if (valid) return null;

        // Authenticate before inspecting claims: malformed or forged payloads must not
        // be diagnosed as trusted issuer/audience/lifetime failures.
        try
        {
            var builder = new PasetoBuilder().Use(ProtocolVersion.V4,
                mode == PasetoMode.Local ? Purpose.Local : Purpose.Public);
            if (mode == PasetoMode.Local) builder.WithSharedKey(keyBytes);
            else builder.WithPublicKey(keyBytes);
            var verified = builder.Decode(token, new PasetoTokenValidationParameters
            { ValidateLifetime = false, ValidateIssuer = false, ValidateAudience = false });
            if (!verified.IsValid)
                return "PASETO signature or decryption failed, or the token is malformed. Check the validation key and token source.";
            var claims = mode == PasetoMode.Local ? GetLocalTokenClaims(token, key) : GetTokenClaims(token);
            if (claims.GetValueOrDefault("iss") != issuer)
                return "PASETO issuer mismatch. Check Linbik:JwtIssuer and the token issuer.";
            if (claims.GetValueOrDefault("aud") != audience)
                return "PASETO audience mismatch. Check Linbik:PackageName (Linbik__PackageName) and the target service registration.";
            if (claims.TryGetValue("exp", out var expiry) &&
                DateTimeOffset.TryParse(expiry, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var expires) && expires <= DateTimeOffset.UtcNow)
                return "PASETO token has expired. Obtain a new token and check server clocks.";
            return "PASETO lifetime or required claims are invalid. Check exp, nbf, iat and server clocks.";
        }
        catch
        {
            return "PASETO signature or decryption failed, or the token is malformed. Check the validation key and token source.";
        }
    }

    private const int Ed25519SignatureSize = 64;

    /// <inheritdoc/>
    public Task<string> CreateTokenAsync(
        Claim[] claims,
        string privateKeyBase64,
        string audience,
        int expirationMinutes = 60,
        string? keyId = null)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyBase64);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

        var now = DateTime.UtcNow;

        var builder = new PasetoBuilder()
            .Use(ProtocolVersion.V4, Purpose.Public)
            .WithSecretKey(privateKeyBytes)
            .Issuer(LinbikDefaults.Issuer)
            .Audience(audience)
            .Expiration(now.AddMinutes(expirationMinutes))
            .IssuedAt(now.AddSeconds(-60));  // ve saatler sapıyorsa ilk istek nbf nedeniyle reddedilmez.

        // Add extra claims
        foreach (var claim in claims)
        {
            builder.AddClaim(claim.Type, claim.Value);
        }

        // Embed kid + iss into the footer for key rotation support
        if (!string.IsNullOrEmpty(keyId))
        {
            var footer = JsonSerializer.Serialize(new { kid = keyId, iss = LinbikDefaults.Issuer });
            builder.AddFooter(footer);
        }

        return Task.FromResult(builder.Encode());
    }

    /// <inheritdoc/>
    public Task<bool> ValidateTokenAsync(
        string token,
        string publicKeyBase64,
        string expectedAudience,
        string expectedIssuer = LinbikDefaults.Issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyBase64);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedAudience);

        try
        {
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);

            var result = new PasetoBuilder()
                .Use(ProtocolVersion.V4, Purpose.Public)
                .WithPublicKey(publicKeyBytes)
                .Decode(token, new PasetoTokenValidationParameters
                {
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                    ValidIssuer = expectedIssuer,
                    ValidateAudience = true,
                    ValidAudience = expectedAudience,
                });

            return Task.FromResult(result.IsValid);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// İmza doğrulaması yapılmaz — yalnızca loglama/debug için kullanın.
    /// PASETO v4.public: <c>v4.public.BASE64URL(payload||sig)[.BASE64URL(footer)]</c>
    /// Payload ile imza arasındaki sınır: son 64 byte imzadır.
    /// </remarks>
    public Dictionary<string, string> GetTokenClaims(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var parts = token.Split('.');
        // PASETO: v4 . public . <payload_and_sig> [. <footer>]
        if (parts.Length < 3)
            return [];

        // v4.local tokens are encrypted — cannot be decoded without the shared key.
        // Use GetLocalTokenClaims(token, sharedKeyBase64) for v4.local tokens.
        if (string.Equals(parts[1], "local", StringComparison.Ordinal))
            return [];

        try
        {
            var payloadAndSig = Base64UrlDecode(parts[2]);
            if (payloadAndSig.Length <= Ed25519SignatureSize)
                return [];

            // Payload = everything except the last 64 signature bytes
            var payloadBytes = payloadAndSig[..^Ed25519SignatureSize];
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);

            using var doc = JsonDocument.Parse(payloadJson);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? string.Empty
                    : prop.Value.GetRawText();
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc/>
    public (string PrivateKeyBase64, string PublicKeyBase64) GenerateKeyPair()
    {
        // Paseto.NET v4.public Ed25519 anahtarı için 32-byte seed gerektirir.
        var seed = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(seed);

        var keyPair = new PasetoBuilder()
            .Use(ProtocolVersion.V4, Purpose.Public)
            .GenerateAsymmetricKeyPair(seed);

        return (
            Convert.ToBase64String(keyPair.SecretKey.Key.ToArray()),
            Convert.ToBase64String(keyPair.PublicKey.Key.ToArray())
        );
    }

    // ─── PASETO v4.local (XChaCha20-Poly1305 symmetric) ─────────────────────

    /// <inheritdoc/>
    public Task<string> CreateLocalTokenAsync(
        Claim[] claims,
        string sharedKeyBase64,
        string audience,
        int expirationMinutes = 60,
        string? keyId = null)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKeyBase64);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        var sharedKeyBytes = Convert.FromBase64String(sharedKeyBase64);

        var now = DateTime.UtcNow;

        var builder = new PasetoBuilder()
            .Use(ProtocolVersion.V4, Purpose.Local)
            .WithSharedKey(sharedKeyBytes)
            .Issuer(LinbikDefaults.Issuer)
            .Audience(audience)
            .Expiration(now.AddMinutes(expirationMinutes))
            .IssuedAt(now.AddSeconds(-60)); // 60s clock-skew toleransı

        foreach (var claim in claims)
        {
            builder.AddClaim(claim.Type, claim.Value);
        }

        if (!string.IsNullOrEmpty(keyId))
        {
            var footer = JsonSerializer.Serialize(new { kid = keyId, iss = LinbikDefaults.Issuer });
            builder.AddFooter(footer);
        }

        return Task.FromResult(builder.Encode());
    }

    /// <inheritdoc/>
    public Task<bool> ValidateLocalTokenAsync(
        string token,
        string sharedKeyBase64,
        string expectedAudience,
        string expectedIssuer = LinbikDefaults.Issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKeyBase64);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedAudience);

        try
        {
            var sharedKeyBytes = Convert.FromBase64String(sharedKeyBase64);

            var result = new PasetoBuilder()
                .Use(ProtocolVersion.V4, Purpose.Local)
                .WithSharedKey(sharedKeyBytes)
                .Decode(token, new PasetoTokenValidationParameters
                {
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                    ValidIssuer = expectedIssuer,
                    ValidateAudience = true,
                    ValidAudience = expectedAudience,
                });

            return Task.FromResult(result.IsValid);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public Dictionary<string, string> GetLocalTokenClaims(string token, string sharedKeyBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKeyBase64);

        try
        {
            var sharedKeyBytes = Convert.FromBase64String(sharedKeyBase64);

            // Decode without strict validation; caller is expected to have validated
            // the token first (or only use this for logging/debug).
            var result = new PasetoBuilder()
                .Use(ProtocolVersion.V4, Purpose.Local)
                .WithSharedKey(sharedKeyBytes)
                .Decode(token, new PasetoTokenValidationParameters
                {
                    ValidateLifetime = false,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                });

            if (!result.IsValid || result.Paseto?.Payload is null)
                return [];

            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kvp in result.Paseto.Payload)
            {
                dict[kvp.Key] = kvp.Value switch
                {
                    null => string.Empty,
                    string s => s,
                    JsonElement je => je.ValueKind == JsonValueKind.String
                        ? (je.GetString() ?? string.Empty)
                        : je.GetRawText(),
                    _ => JsonSerializer.Serialize(kvp.Value),
                };
            }
            return dict;
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc/>
    public string GenerateSymmetricKey()
    {
        var keyPair = new PasetoBuilder()
            .Use(ProtocolVersion.V4, Purpose.Local)
            .GenerateSymmetricKey();

        return Convert.ToBase64String(keyPair.Key.ToArray());
    }

    // ─── Base64Url helper ───────────────────────────────────────────────────
    private static byte[] Base64UrlDecode(string base64Url)
    {
        var s = base64Url.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s,
        };
        return Convert.FromBase64String(s);
    }
}
