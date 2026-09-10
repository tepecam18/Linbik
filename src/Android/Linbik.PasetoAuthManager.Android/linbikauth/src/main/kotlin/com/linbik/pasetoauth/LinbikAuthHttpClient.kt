package com.linbik.pasetoauth

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException

/** Owns HTTP resources and response decoding independently of Activity lifecycle. */
internal class LinbikAuthHttpClient(
    private val client: OkHttpClient = OkHttpClient.Builder().cookieJar(LinbikSharedCookieJar()).build(),
) {
    suspend fun updateSession(options: LinbikPasetoAuthOptions, refresh: Boolean): Boolean = withContext(Dispatchers.IO) {
        val path = if (refresh) options.refreshPath else options.logoutPath
        val request = Request.Builder().url(options.backendBaseUrl.trimEnd('/') + path).apply {
            if (refresh) post(ByteArray(0).toRequestBody())
        }.build()
        try {
            client.newCall(request).execute().use { it.isSuccessful }
        } catch (e: CancellationException) {
            throw e
        } catch (e: IOException) {
            false
        }
    }

    fun getJson(url: String): JSONObject {
        return client.newCall(Request.Builder().url(url).build()).execute().use { response ->
            val bodyText = response.body?.string().orEmpty()

            if (!response.isSuccessful) {
                val errorMsg = try {
                    JSONObject(bodyText).optJSONObject("friendlyMessage")?.optString("message")
                } catch (e: Exception) {
                    null
                } ?: "Sunucu hatası (${response.code})."
                throw IllegalStateException(errorMsg)
            }

            if (bodyText.isBlank()) {
                throw IllegalStateException("Sunucudan boş yanıt döndü.")
            }

            // HTML tespiti: ActionResultType='Json' yapılmadığında backend HTML döner.
            val trimmedBody = bodyText.trim()
            if (trimmedBody.startsWith("<!DOCTYPE", ignoreCase = true) || trimmedBody.startsWith("<html", ignoreCase = true)) {
                throw IllegalStateException(
                    "Sunucu JSON yerine HTML döndü. Backend'de bu client için " +
                        "ActionResultType='Json' olarak ayarlandığından emin olun.",
                )
            }

            try {
                JSONObject(bodyText)
            } catch (e: Exception) {
                throw IllegalStateException("Sunucu geçersiz bir yanıt döndü (JSON bekleniyordu).")
            }
        }
    }
}
