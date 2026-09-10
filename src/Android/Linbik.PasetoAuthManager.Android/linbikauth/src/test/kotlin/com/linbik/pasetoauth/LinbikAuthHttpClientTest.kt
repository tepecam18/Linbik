package com.linbik.pasetoauth

import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.Protocol
import okhttp3.Response
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.IOException

class LinbikAuthHttpClientTest {
    @Test
    fun refreshUsesPostAndConfiguredPath() = runBlocking {
        val client = OkHttpClient.Builder().addInterceptor { chain ->
            assertEquals("POST", chain.request().method)
            assertEquals("https://example.com/custom/refresh", chain.request().url.toString())
            Response.Builder().request(chain.request()).protocol(Protocol.HTTP_1_1)
                .code(200).message("OK").body("".toResponseBody()).build()
        }.build()
        val options = LinbikPasetoAuthOptions("https://example.com/", refreshPath = "/custom/refresh")
        assertTrue(LinbikAuthHttpClient(client).updateSession(options, refresh = true))
    }

    @Test
    fun logoutKeepsGetContract() = runBlocking {
        val client = OkHttpClient.Builder().addInterceptor { chain ->
            assertEquals("GET", chain.request().method)
            assertEquals("/api/Linbik/logout", chain.request().url.encodedPath)
            Response.Builder().request(chain.request()).protocol(Protocol.HTTP_1_1)
                .code(200).message("OK").body("".toResponseBody()).build()
        }.build()
        assertTrue(LinbikAuthHttpClient(client).updateSession(LinbikPasetoAuthOptions("https://example.com"), refresh = false))
    }

    @Test
    fun networkFailureReturnsFalse() = runBlocking {
        val client = OkHttpClient.Builder().addInterceptor { throw IOException("offline") }.build()
        assertFalse(LinbikAuthHttpClient(client).updateSession(LinbikPasetoAuthOptions("https://example.com"), refresh = true))
    }
}
