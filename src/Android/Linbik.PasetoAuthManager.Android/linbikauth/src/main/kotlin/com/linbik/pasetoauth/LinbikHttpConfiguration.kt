package com.linbik.pasetoauth

import okhttp3.OkHttpClient

/** Process-wide transport used by both the auth Activity and session operations. */
internal object LinbikHttpConfiguration {
    @Volatile
    var client: OkHttpClient = OkHttpClient.Builder()
        .cookieJar(LinbikSharedCookieJar())
        .build()
        private set

    fun configure(baseClient: OkHttpClient) {
        // Keep interceptors, timeouts, dispatcher, TLS and connection pool settings.
        // Auth/PKCE cookies must continue to use the shared persistent cookie jar.
        client = baseClient.newBuilder().cookieJar(LinbikSharedCookieJar()).build()
    }
}
