namespace Linbik.Core.Models;

/// <summary>
/// Ed25519 anahtar çifti: key ID, opsiyonel expiry ve Base64-kodlu ham baytlar.
/// Signing tarafı (Linbik.Api) <see cref="PrivateKeyBase64"/> dolu olarak tutar;
/// doğrulama tarafı (Linbik.Server) yalnızca <see cref="PublicKeyBase64"/> kullanır.
/// </summary>
public sealed class PasetoKey
{
    /// <summary>Anahtar tanımlayıcı (ör. "2024-01-key1"). PASETO footer'ında <c>kid</c> olarak taşınır.</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>Ed25519 32-byte public key, Base64-encoded.</summary>
    public string PublicKeyBase64 { get; set; } = string.Empty;

    /// <summary>Ed25519 64-byte private key (seed||public), Base64-encoded. Yalnızca signing tarafında dolu olur.</summary>
    public string? PrivateKeyBase64 { get; set; }

    /// <summary>Bu anahtarın geçerliliğinin sona ereceği UTC zamanı. Null ise süresiz geçerlidir.</summary>
    public DateTime? NotAfter { get; set; }

    /// <summary>Anahtarın süresi dolmuşsa true döner.</summary>
    public bool IsExpired => NotAfter.HasValue && NotAfter.Value < DateTime.UtcNow;
}
