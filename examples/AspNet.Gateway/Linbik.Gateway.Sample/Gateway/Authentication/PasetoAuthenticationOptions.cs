using Microsoft.AspNetCore.Authentication;

namespace Linbik.Gateway.Sample.Gateway.Authentication;

/// <summary>
/// Token kaynağı: cookie veya Authorization Bearer header.
/// </summary>
public enum PasetoTokenSource
{
    /// <summary>Cookie'den oku (Self scheme için).</summary>
    Cookie,
    /// <summary><c>Authorization: Bearer &lt;token&gt;</c> header'ından oku.</summary>
    Bearer
}

/// <summary>
/// <see cref="PasetoAuthenticationHandler"/> için yapılandırma seçenekleri.
/// Her scheme farklı bir <see cref="PasetoAuthenticationOptions"/> örneği alır.
/// </summary>
public sealed class PasetoAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Ed25519 public key — Base64 (32 byte).
    /// <c>appsettings.json</c>'daki <c>LinbikGateway:Auth:&lt;Scheme&gt;:PublicKey</c> ile beslenir.
    /// KeylessMode'da handler token'ı "always valid" kabul eder (geliştirme ortamı).
    /// </summary>
    public string PublicKeyBase64 { get; set; } = string.Empty;

    /// <summary>Token'ın okunacağı kaynak.</summary>
    public PasetoTokenSource TokenSource { get; set; } = PasetoTokenSource.Bearer;

    /// <summary>
    /// <see cref="PasetoTokenSource.Cookie"/> seçildiğinde okunacak cookie adı.
    /// Varsayılan: <see cref="Core.LinbikDefaults.AuthTokenCookie"/>.
    /// </summary>
    public string CookieName { get; set; } = Core.LinbikDefaults.AuthTokenCookie;

    /// <summary>
    /// Token'da beklenen audience değeri.
    /// Genellikle gateway'in package name'i veya hedef servis adı.
    /// </summary>
    public string ExpectedAudience { get; set; } = string.Empty;

    /// <summary>
    /// Beklenen issuer. Varsayılan: <see cref="Core.LinbikDefaults.Issuer"/>.
    /// </summary>
    public string ExpectedIssuer { get; set; } = Core.LinbikDefaults.Issuer;

    /// <summary>
    /// Development/KeylessMode: public key olmadan tüm token'lar geçerli sayılır.
    /// ÜRETİMDE KAPATILMALI.
    /// </summary>
    public bool KeylessMode { get; set; } = false;
}
