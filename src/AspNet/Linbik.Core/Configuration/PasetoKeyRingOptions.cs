using Linbik.Core.Models;

namespace Linbik.Core.Configuration;

/// <summary>
/// PASETO anahtar halkası konfigürasyonu.
/// appsettings.json'dan bağlanır: <c>Linbik:PasetoKeyRing</c>
/// </summary>
public sealed class PasetoKeyRingOptions
{
    /// <summary>Şu anda aktif olarak kullanılan anahtarın <see cref="PasetoKey.KeyId"/> değeri.</summary>
    public string ActiveKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Tüm Ed25519 anahtar çiftleri listesi.
    /// Eski anahtarlar OverlapWindow boyunca doğrulama için tutulur.
    /// </summary>
    public List<PasetoKey> Keys { get; set; } = [];

    /// <summary>
    /// Anahtar rotasyonu sırasında eski anahtarın geçerli kalacağı süre.
    /// Uçuşta olan token'ların expire olmadan önce doğrulanabilmesini sağlar.
    /// </summary>
    public TimeSpan OverlapWindow { get; set; } = TimeSpan.FromHours(24);
}
