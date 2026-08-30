package com.linbik.pasetoauth

import android.webkit.CookieManager
import okhttp3.Cookie
import okhttp3.CookieJar
import okhttp3.HttpUrl

/**
 * Giriş akışında ve sonrasındaki API çağrılarında kullanılan oturum çerezlerini (authToken,
 * linbik_refresh vb.) Android'in sistem düzeyindeki `CookieManager`'ı ile senkronize eder.
 *
 * Bu sayede:
 * 1. Giriş sırasında OkHttp ile alınan çerezler kalıcı (persistent) hale gelir.
 * 2. Uygulama içinde açılacak bir WebView, bu çerezleri otomatik olarak kullanır.
 * 3. Uygulamanızın kendi OkHttp istemcisi de bu çerezleri paylaşabilir:
 *
 * ```kotlin
 * val client = OkHttpClient.Builder()
 *     .cookieJar(LinbikSharedCookieJar())
 *     .build()
 * ```
 */
class LinbikSharedCookieJar : CookieJar {
    private val cookieManager get() = CookieManager.getInstance()

    override fun saveFromResponse(url: HttpUrl, cookies: List<Cookie>) {
        val urlString = url.toString()
        cookies.forEach { cookie ->
            cookieManager.setCookie(urlString, cookie.toString())
        }
        cookieManager.flush()
    }

    override fun loadForRequest(url: HttpUrl): List<Cookie> {
        val cookieHeader = cookieManager.getCookie(url.toString()) ?: return emptyList()
        return cookieHeader.split(';')
            .mapNotNull { it.trim().takeIf(String::isNotEmpty) }
            .mapNotNull { Cookie.parse(url, it) }
    }
}
