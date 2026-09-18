package com.linbik.pasetoauth

import java.io.IOException
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.runBlocking
import okhttp3.CookieJar
import okhttp3.OkHttpClient
import okhttp3.Protocol
import okhttp3.Response
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.After
import org.junit.Assert.*
import org.junit.Test

class LinbikHttpConfigurationTest {
    @After
    fun reset() {
        LinbikPasetoAuthClient.configureHttpClient(OkHttpClient())
    }

    @Test
    fun retainsInterceptorsAndTransportSettingsWithoutMutatingCallerClient() {
        val base = OkHttpClient.Builder().callTimeout(17, TimeUnit.SECONDS)
            .addInterceptor { throw IOException("test") }.build()
        LinbikPasetoAuthClient.configureHttpClient(base)
        val configured = LinbikHttpConfiguration.client
        assertSame(base.interceptors.single(), configured.interceptors.single())
        assertSame(base.dispatcher, configured.dispatcher)
        assertSame(base.connectionPool, configured.connectionPool)
        assertEquals(17_000, configured.callTimeoutMillis)
        assertTrue(configured.cookieJar is LinbikSharedCookieJar)
        assertSame(CookieJar.NO_COOKIES, base.cookieJar)
    }

    @Test
    fun existingActivityTransportAndSessionClientUseConfiguredInterceptor() = runBlocking {
        val activityTransport = LinbikAuthHttpClient()
        val sessionClient = LinbikPasetoAuthClient()
        val paths = mutableListOf<String>()
        LinbikPasetoAuthClient.configureHttpClient(OkHttpClient.Builder().addInterceptor { chain ->
            paths.add(chain.request().url.encodedPath)
            if (chain.request().url.encodedPath in listOf("/login", "/callback")) {
                // Stop before Android's JSONObject parsing; verify both Activity HTTP calls
                // traverse the configured interceptor without contacting a real server.
                throw IOException("intercepted")
            }
            Response.Builder().request(chain.request()).protocol(Protocol.HTTP_1_1)
                .code(200).message("OK").body("".toResponseBody()).build()
        }.build())
        for (path in listOf("/login", "/callback")) {
            try {
                activityTransport.getJson("https://example.com$path")
                fail("Expected interceptor to stop request")
            } catch (_: IOException) { }
        }
        val options = LinbikPasetoAuthOptions("https://example.com")
        assertTrue(sessionClient.refreshToken(options))
        assertTrue(sessionClient.signOut(options))
        assertEquals(listOf("/login", "/callback", options.refreshPath, options.logoutPath), paths)
    }
}
