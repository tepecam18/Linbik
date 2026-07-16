using System.Security.Claims;

namespace Linbik.Core.Services.Interfaces;

/// <summary>
/// PASETO v4.public token üretimi ve doğrulaması için Ed25519 tabanlı yardımcı arayüz.
/// Servisler arası (apps) ve kullanıcı-servis (delegated) token'larını yönetir.
/// </summary>
public interface IPasetoHelper
{
    /// <summary>
    /// Ed25519 private key ile imzalanmış bir PASETO v4.public token üretir.
    /// </summary>
    /// <param name="claims">Token içine dahil edilecek claim'ler.</param>
    /// <param name="privateKeyBase64">Base64-kodlu 64-byte Ed25519 private key (seed||public).</param>
    /// <param name="audience">Token'ın hedef servisi (audience claim).</param>
    /// <param name="expirationMinutes">Token geçerlilik süresi dakika cinsinden (varsayılan: 60).</param>
    /// <param name="keyId">Footer'a eklenecek anahtar tanımlayıcı (<c>kid</c>). Null ise footer eklenmez.</param>
    /// <returns>PASETO v4.public token string'i.</returns>
    Task<string> CreateTokenAsync(
        Claim[] claims,
        string privateKeyBase64,
        string audience,
        int expirationMinutes = 60,
        string? keyId = null);

    /// <summary>
    /// Ed25519 public key ile bir PASETO v4.public token'ı doğrular.
    /// Lifetime, issuer ve audience kontrolleri yapılır.
    /// </summary>
    /// <param name="token">Doğrulanacak PASETO token string'i.</param>
    /// <param name="publicKeyBase64">Base64-kodlu 32-byte Ed25519 public key.</param>
    /// <param name="expectedAudience">Beklenen audience değeri (bu servisin package name'i).</param>
    /// <param name="expectedIssuer">Beklenen issuer değeri (varsayılan: <see cref="LinbikDefaults.Issuer"/>).</param>
    /// <returns>Token geçerliyse true, değilse false.</returns>
    Task<bool> ValidateTokenAsync(
        string token,
        string publicKeyBase64,
        string expectedAudience,
        string expectedIssuer = LinbikDefaults.Issuer);

    /// <summary>
    /// İmza doğrulaması yapmadan token payload'ındaki claim'leri döner.
    /// Yalnızca loglama, debug veya izin gerektirmeyen ön-kontroller için kullanın.
    /// Güvenlik kararları için <see cref="ValidateTokenAsync"/> kullanılmalıdır.
    /// </summary>
    /// <param name="token">PASETO token string'i.</param>
    /// <returns>Claim anahtar-değer çiftleri; token parse edilemezse boş dictionary.</returns>
    Dictionary<string, string> GetTokenClaims(string token);

    /// <summary>
    /// Yeni bir Ed25519 anahtar çifti üretir.
    /// Dönen private key 64-byte (seed||public), public key 32-byte, her ikisi de Base64-kodludur.
    /// </summary>
    /// <returns>(PrivateKeyBase64, PublicKeyBase64) çifti.</returns>
    (string PrivateKeyBase64, string PublicKeyBase64) GenerateKeyPair();

    // ─── PASETO v4.local (XChaCha20-Poly1305 symmetric) ─────────────────────

    /// <summary>
    /// Simetrik shared key ile şifrelenmiş bir PASETO v4.local token üretir.
    /// Payload imzalanmaz, şifrelenir; aynı sınırın içinde üretilip doğrulanacak
    /// cookie/session senaryoları için uygundur.
    /// </summary>
    /// <param name="claims">Token içine dahil edilecek claim'ler.</param>
    /// <param name="sharedKeyBase64">Base64-kodlu 32-byte simetrik şifreleme anahtarı.</param>
    /// <param name="audience">Token'ın hedef servisi (audience claim).</param>
    /// <param name="expirationMinutes">Token geçerlilik süresi dakika cinsinden (varsayılan: 60).</param>
    /// <param name="keyId">Footer'a eklenecek anahtar tanımlayıcı (<c>kid</c>). Null ise footer eklenmez.</param>
    /// <returns>PASETO v4.local token string'i.</returns>
    Task<string> CreateLocalTokenAsync(
        System.Security.Claims.Claim[] claims,
        string sharedKeyBase64,
        string audience,
        int expirationMinutes = 60,
        string? keyId = null);

    /// <summary>
    /// Simetrik shared key ile bir PASETO v4.local token'ı çözer ve doğrular.
    /// Lifetime, issuer ve audience kontrolleri yapılır.
    /// </summary>
    /// <param name="token">Doğrulanacak PASETO v4.local token string'i.</param>
    /// <param name="sharedKeyBase64">Base64-kodlu 32-byte simetrik şifreleme anahtarı.</param>
    /// <param name="expectedAudience">Beklenen audience değeri.</param>
    /// <param name="expectedIssuer">Beklenen issuer değeri (varsayılan: <see cref="LinbikDefaults.Issuer"/>).</param>
    /// <returns>Token geçerliyse true, değilse false.</returns>
    Task<bool> ValidateLocalTokenAsync(
        string token,
        string sharedKeyBase64,
        string expectedAudience,
        string expectedIssuer = LinbikDefaults.Issuer);

    /// <summary>
    /// v4.local token payload'ını shared key ile çözer ve claim'leri döner.
    /// İmza/MAC doğrulaması Paseto kütüphanesi tarafından yapılır; geçersiz token boş dictionary döner.
    /// </summary>
    /// <param name="token">PASETO v4.local token string'i.</param>
    /// <param name="sharedKeyBase64">Base64-kodlu 32-byte simetrik şifreleme anahtarı.</param>
    /// <returns>Claim anahtar-değer çiftleri; token çözülemezse boş dictionary.</returns>
    Dictionary<string, string> GetLocalTokenClaims(string token, string sharedKeyBase64);

    /// <summary>
    /// PASETO v4.local için yeni bir 32-byte simetrik anahtar üretir (Base64-kodlu).
    /// </summary>
    /// <returns>Base64-kodlu 32-byte simetrik anahtar.</returns>
    string GenerateSymmetricKey();
}
