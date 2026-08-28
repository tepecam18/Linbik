package com.linbik.pasetoauth

import android.webkit.CookieManager
import okhttp3.Cookie
import okhttp3.CookieJar
import okhttp3.HttpUrl

/**
 * [LinbikAuthActivity] içindeki WebView'ın oturum çerezlerini (authToken, linbik_refresh vb.)
 * uygulamanızın kendi OkHttp istemcisiyle paylaşır. Böylece giriş WebView'da tamamlandıktan
 * sonra, backend'inize yapacağınız normal API çağrıları da aynı oturumu kullanır:
 *
 * ```kotlin
 * val client = OkHttpClient.Builder()
 *     .cookieJar(LinbikWebViewCookieJar())
 *     .build()
 * ```
 *
 * Not: `HttpOnly` çerezler JavaScript'ten gizlenir ama Android'in `CookieManager` API'sinden
 * (native kod) gizlenmez — bu yüzden bu köprü `authToken`/`linbik_refresh` gibi HttpOnly
 * çerezleri de doğru şekilde okuyabilir.
 */
class LinbikWebViewCookieJar : CookieJar {
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
