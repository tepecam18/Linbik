package com.linbik.pasetoauth

/**
 * Linbik.PasetoAuthManager (ASP.NET) kullanan kendi backend'inize karşı yapılandırma.
 * Varsayılan path'ler [Linbik.PasetoAuthManager.Configuration.PasetoAuthOptions]'ın
 * varsayılanlarıyla birebir eşleşir — backend'inizde özelleştirdiyseniz burada da güncelleyin.
 *
 * ÖNEMLİ: Linbik.App'te (veya Linbik.Api'de) bu uygulama için oluşturduğunuz Client'ın
 * `RedirectUri` alanı, uygulamanızın `applicationId`'sine dayalı özel URI şemasıyla TAM
 * olarak eşleşmelidir: `{applicationId}://oauth/callback` (bkz. README → "Nasıl Çalışır").
 *
 * @property backendBaseUrl Kendi ASP.NET backend'inizin adresi, örn. "https://10.0.2.2:7020"
 *   (Android emülatöründe "localhost" yerine "10.0.2.2" kullanılmalıdır).
 * @property clientName `Linbik:Clients` altında `ActionResultType: "Json"` ile tanımlı client'ın
 *   `Name` değeri (bkz. README → "Backend Yapılandırması"). Boş bırakılırsa backend Keyless Mode
 *   ile ilk client'ı kullanmaya çalışır (bu genelde Json modunda OLMAZ — mobil için açıkça bir
 *   client tanımlamanız önerilir).
 * @property returnPath Giriş başarılı olduktan sonra backend'in `redirectPath` alanında
 *   döneceği ek bilgi (isteğe bağlı, backend'inizde nasıl kullandığınıza bağlı).
 */
data class LinbikPasetoAuthOptions(
    val backendBaseUrl: String,
    val clientName: String? = null,
    val returnPath: String? = null,
    val loginPath: String = "/api/Linbik/login",
    val loginCallbackPath: String = "/api/Linbik/callback",
    val logoutPath: String = "/api/Linbik/logout",
    val refreshPath: String = "/api/Linbik/refresh",
)
