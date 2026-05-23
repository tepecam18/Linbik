using Linbik.Core.Models;

namespace Linbik.Core.Services.Interfaces;

/// <summary>
/// PASETO anahtar halkası: aktif anahtar yönetimi ve rotasyon desteği.
/// Signing tarafı (Linbik.Api) tarafından kullanılır.
/// </summary>
public interface IPasetoKeyRing
{
    /// <summary>Token imzalamada kullanılacak aktif anahtar.</summary>
    PasetoKey Active { get; }

    /// <summary>
    /// Verilen <paramref name="keyId"/> ile anahtar bulunabiliyorsa true döner.
    /// Süresi dolmuş anahtarlar OverlapWindow içinde hâlâ geri döner (uçuşta token doğrulama için).
    /// </summary>
    bool TryGet(string keyId, out PasetoKey key);

    /// <summary>
    /// Yeni bir anahtarı aktif yapar; eski aktif anahtar OverlapWindow kadar geçerli kalır.
    /// Thread-safe: birden fazla istek eş zamanlı çağırabilir.
    /// </summary>
    Task RotateAsync(PasetoKey newKey, CancellationToken cancellationToken = default);

    /// <summary>Süresi dolmamış tüm anahtarları (aktif dahil) döner.</summary>
    IReadOnlyList<PasetoKey> GetValid();
}
