package com.linbik.pasetoauth

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.browser.customtabs.CustomTabsIntent
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.net.URLEncoder

/**
 * Linbik.PasetoAuthManager destekli backend'e karşı Custom Tabs + deep link tabanlı giriş
 * akışını yürütür. Doğrudan başlatmak yerine [LinbikPasetoAuthClient] üzerinden kullanın.
 *
 * RFC 8252 (OAuth 2.0 for Native Apps) uyarınca yetkilendirme sayfası bir WebView içinde
 * DEĞİL, Custom Tabs (harici user-agent) ile açılır — bu, Android'in yüklü olan Linbik.Mobil
 * uygulamasına App Link ile yönlendirme yapabilmesi için de gereklidir (WebView bunu desteklemez).
 *
 * Nasıl çalışır (bkz. README → "Nasıl Çalışır"):
 * 1. `{backendBaseUrl}{loginPath}?name=...` adresine kendi OkHttp istemcimizle istek atılır;
 *    dönen JSON'daki `redirectPath` (Linbik'in gerçek giriş/onay sayfası) Custom Tabs'te açılır.
 * 2. Kullanıcı Linbik'te oturum açar/onaylar (yüklüyse Linbik.Mobil'e App Link ile yönlenebilir).
 * 3. Linbik, tarayıcıyı `{applicationId}://oauth/callback?code=...` adresine yönlendirir; bu,
 *    [LinbikRedirectActivity] tarafından yakalanıp bu Activity'ye iletilir ([onNewIntent]).
 * 4. `code` ile backend'in callback endpoint'ine (yine kendi OkHttp istemcimizle) istek atılır;
 *    dönen JSON (`LoginCallbackResponse`) ayrıştırılıp sonuç uygulamaya döndürülür.
 * 5. Backend'in Set-Cookie ile yazdığı oturum çerezleri (PKCE `code_verifier` dahil) 1. ve 4.
 *    adımlar arasında `CookieManager` üzerinden kalıcıdır — bkz. [LinbikSharedCookieJar].
 */
internal class LinbikAuthActivity : ComponentActivity() {

    private val job = Job()
    private val scope = CoroutineScope(Dispatchers.Main + job)
    private val httpClient by lazy {
        OkHttpClient.Builder()
            .cookieJar(LinbikSharedCookieJar())
            .build()
    }

    private lateinit var options: LinbikPasetoAuthOptions

    /** Custom Tabs açıldıktan sonra true olur; kullanıcı tamamlamadan geri dönerse iptal sayılır. */
    private var authInFlight = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        options = intent.toOptions()

        if (savedInstanceState == null) {
            startLoginRequest()
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        val uri = intent.data
        authInFlight = false
        if (uri == null) {
            finishCancelled()
        } else {
            handleRedirectUri(uri)
        }
    }

    override fun onResume() {
        super.onResume()
        // Custom Tabs açıldıktan sonra bir tamamlama intent'i almadan buraya tekrar
        // gelindiyse (geri tuşu / sekme kapatıldı) kullanıcı akışı iptal etmiştir.
        if (authInFlight) {
            finishCancelled()
        }
    }

    private fun startLoginRequest() {
        val nameParam = options.clientName?.let { "&name=${encodeUrlParam(it)}" } ?: ""
        val returnParam = options.returnPath?.let { "&returnPath=${encodeUrlParam(it)}" } ?: ""
        val loginUrl = "${options.backendBaseUrl.trimEnd('/')}${options.loginPath}?_=1$nameParam$returnParam"

        scope.launch {
            try {
                val json = withContext(Dispatchers.IO) { getJson(loginUrl) }
                handleLoginResponse(json)
            } catch (e: Exception) {
                Log.w(TAG, "Login request failed for $loginUrl", e)
                finishError(e.message ?: "Ağ hatası oluştu.")
            }
        }
    }

    private fun handleLoginResponse(json: JSONObject) {
        if (!json.optBoolean("isSuccess", false)) {
            finishError(friendlyMessage(json) ?: "Giriş başlatılamadı.")
            return
        }
        val redirectPath = json.optJSONObject("data")?.optString("redirectPath")
        if (redirectPath.isNullOrEmpty()) {
            finishError(
                "Sunucu bir yönlendirme adresi döndürmedi. Backend'te bu client'ın " +
                    "ActionResultType='Json' olarak ayarlandığından emin olun.",
            )
            return
        }
        launchCustomTab(redirectPath)
    }

    private fun launchCustomTab(url: String) {
        authInFlight = true
        try {
            CustomTabsIntent.Builder().build().launchUrl(this, Uri.parse(url))
        } catch (e: Exception) {
            Log.w(TAG, "Custom Tabs unavailable, falling back to ACTION_VIEW", e)
            try {
                startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
            } catch (e2: Exception) {
                authInFlight = false
                finishError("Bir tarayıcı bulunamadı: ${e2.message}")
            }
        }
    }

    private fun handleRedirectUri(uri: Uri) {
        val error = uri.getQueryParameter("error")
        if (!error.isNullOrEmpty()) {
            finishError(uri.getQueryParameter("error_description") ?: error)
            return
        }
        val code = uri.getQueryParameter("code")
        if (code.isNullOrEmpty()) {
            finishError("Yetkilendirme kodu alınamadı.")
            return
        }

        val callbackUrl = "${options.backendBaseUrl.trimEnd('/')}${options.loginCallbackPath}" +
            "?code=${encodeUrlParam(code)}"

        scope.launch {
            try {
                val json = withContext(Dispatchers.IO) { getJson(callbackUrl) }
                handleCallbackResponse(json)
            } catch (e: Exception) {
                Log.w(TAG, "Callback request failed for $callbackUrl", e)
                finishError(e.message ?: "Ağ hatası oluştu.")
            }
        }
    }

    private fun getJson(url: String): JSONObject {
        val response = httpClient.newCall(Request.Builder().url(url).build()).execute()
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

        return try {
            JSONObject(bodyText)
        } catch (e: Exception) {
            Log.e(TAG, "Invalid JSON from $url: $bodyText", e)
            throw IllegalStateException("Sunucu geçersiz bir yanıt döndü (JSON bekleniyordu).")
        }
    }

    private fun handleCallbackResponse(json: JSONObject) {
        if (!json.optBoolean("isSuccess", false)) {
            finishError(friendlyMessage(json) ?: "Giriş başarısız.")
            return
        }
        val data = json.optJSONObject("data")
        if (data == null) {
            finishError("Sunucudan kullanıcı bilgisi alınamadı.")
            return
        }
        val integrations = data.optJSONArray("integrations")
        val integrationList = buildList {
            if (integrations != null) {
                for (i in 0 until integrations.length()) add(integrations.optString(i))
            }
        }
        finishSuccess(
            userId = data.optString("userId"),
            userName = data.optString("userName"),
            displayName = data.optString("displayName"),
            integrations = integrationList,
        )
    }

    private fun friendlyMessage(json: JSONObject): String? =
        json.optJSONObject("friendlyMessage")?.optString("message")?.takeIf { it.isNotBlank() }

    private fun finishSuccess(userId: String, userName: String, displayName: String, integrations: List<String>) {
        val result = Intent().apply {
            putExtra(EXTRA_RESULT_TYPE, RESULT_TYPE_SUCCESS)
            putExtra(EXTRA_USER_ID, userId)
            putExtra(EXTRA_USER_NAME, userName)
            putExtra(EXTRA_DISPLAY_NAME, displayName)
            putStringArrayListExtra(EXTRA_INTEGRATIONS, ArrayList(integrations))
        }
        setResult(Activity.RESULT_OK, result)
        finish()
    }

    private fun finishError(message: String) {
        val result = Intent().apply {
            putExtra(EXTRA_RESULT_TYPE, RESULT_TYPE_ERROR)
            putExtra(EXTRA_ERROR_MESSAGE, message)
        }
        setResult(Activity.RESULT_OK, result)
        finish()
    }

    private fun finishCancelled() {
        setResult(Activity.RESULT_CANCELED)
        finish()
    }

    override fun onDestroy() {
        job.cancel()
        super.onDestroy()
    }

    companion object {
        private const val TAG = "LinbikAuth"

        private const val EXTRA_BACKEND_BASE_URL = "linbik.backendBaseUrl"
        private const val EXTRA_CLIENT_NAME = "linbik.clientName"
        private const val EXTRA_RETURN_PATH = "linbik.returnPath"
        private const val EXTRA_LOGIN_PATH = "linbik.loginPath"
        private const val EXTRA_CALLBACK_PATH = "linbik.callbackPath"

        private const val EXTRA_RESULT_TYPE = "linbik.resultType"
        private const val RESULT_TYPE_SUCCESS = "success"
        private const val RESULT_TYPE_ERROR = "error"
        private const val EXTRA_USER_ID = "linbik.userId"
        private const val EXTRA_USER_NAME = "linbik.userName"
        private const val EXTRA_DISPLAY_NAME = "linbik.displayName"
        private const val EXTRA_INTEGRATIONS = "linbik.integrations"
        private const val EXTRA_ERROR_MESSAGE = "linbik.errorMessage"

        fun createIntent(context: Context, options: LinbikPasetoAuthOptions): Intent =
            Intent(context, LinbikAuthActivity::class.java).apply {
                putExtra(EXTRA_BACKEND_BASE_URL, options.backendBaseUrl)
                putExtra(EXTRA_CLIENT_NAME, options.clientName)
                putExtra(EXTRA_RETURN_PATH, options.returnPath)
                putExtra(EXTRA_LOGIN_PATH, options.loginPath)
                putExtra(EXTRA_CALLBACK_PATH, options.loginCallbackPath)
            }

        private fun Intent.toOptions(): LinbikPasetoAuthOptions = LinbikPasetoAuthOptions(
            backendBaseUrl = getStringExtra(EXTRA_BACKEND_BASE_URL) ?: error("backendBaseUrl is required"),
            clientName = getStringExtra(EXTRA_CLIENT_NAME),
            returnPath = getStringExtra(EXTRA_RETURN_PATH),
            loginPath = getStringExtra(EXTRA_LOGIN_PATH) ?: "/api/Linbik/login",
            loginCallbackPath = getStringExtra(EXTRA_CALLBACK_PATH) ?: "/api/Linbik/callback",
        )

        fun parseResult(intent: Intent): LinbikAuthResult = when (intent.getStringExtra(EXTRA_RESULT_TYPE)) {
            RESULT_TYPE_SUCCESS -> LinbikAuthResult.Success(
                userId = intent.getStringExtra(EXTRA_USER_ID).orEmpty(),
                userName = intent.getStringExtra(EXTRA_USER_NAME).orEmpty(),
                displayName = intent.getStringExtra(EXTRA_DISPLAY_NAME).orEmpty(),
                integrations = intent.getStringArrayListExtra(EXTRA_INTEGRATIONS).orEmpty(),
            )
            RESULT_TYPE_ERROR -> LinbikAuthResult.Error(
                intent.getStringExtra(EXTRA_ERROR_MESSAGE) ?: "Bilinmeyen hata.",
            )
            else -> LinbikAuthResult.Cancelled
        }

        private fun encodeUrlParam(value: String): String = URLEncoder.encode(value, "UTF-8")
    }
}
