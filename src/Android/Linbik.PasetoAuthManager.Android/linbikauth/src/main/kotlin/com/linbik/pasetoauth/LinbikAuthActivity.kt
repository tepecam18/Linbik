package com.linbik.pasetoauth

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.browser.customtabs.CustomTabsIntent
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
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
    private val httpClient by lazy { LinbikAuthHttpClient() }

    private lateinit var options: LinbikPasetoAuthOptions

    /** Custom Tabs açıldıktan sonra true olur; kullanıcı tamamlamadan geri dönerse iptal sayılır. */
    private var authInFlight = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val redirectUri = intent.data

        // Öncelik sırası:
        // 1) savedInstanceState — Activity süreç sonlandırılıp aynı örnek olarak yeniden oluşturuldu.
        // 2) redirectUri varsa kalıcı depoda (SharedPreferences) tutulan son options — bazı
        //    Android sürümlerinde/OEM'lerde, LinbikRedirectActivity'nin CLEAR_TOP+SINGLE_TOP ile
        //    yaptığı yönlendirme mevcut örneğe onNewIntent() ile DEĞİL, bu Activity'nin TAMAMEN
        //    YENİ bir örneğini oluşturarak (onCreate, savedInstanceState=null) teslim ediliyor —
        //    gözlemlenen, tekrarlanabilir bir durum. Bu durumda intent yalnızca `data` (redirect
        //    URI) taşır, options extra'larını taşımaz; onSaveInstanceState tabanlı geri yükleme de
        //    devreye giremez çünkü ortada "geri yüklenecek" bir örnek yoktur. launchCustomTab()
        //    içinde diske yazılan son options burada geri okunur.
        // 3) intent extra'ları — normal ilk başlatma (launcher.launch(...) → createIntent(...)).
        val resolvedOptions = savedInstanceState?.toOptionsOrNull()
            ?: (if (redirectUri != null) loadPersistedOptions() else null)
            ?: intent.toOptionsOrNull()

        if (resolvedOptions == null) {
            // Hiçbir kaynaktan options çözülemedi: örn. bu Activity, launcher.launch(...) hiç
            // çağrılmadan doğrudan callback URI'siyle (adb / eski bir bildirim vb.) başlatıldı.
            // Çökmek yerine düzgün bir hata sonucuyla kapanıyoruz.
            setResult(Activity.RESULT_OK, Intent().apply {
                putExtra(EXTRA_RESULT_TYPE, RESULT_TYPE_ERROR)
                putExtra(EXTRA_ERROR_MESSAGE, "Giriş oturumu bulunamadı. Lütfen tekrar giriş yapmayı deneyin.")
            })
            finish()
            return
        }
        options = resolvedOptions

        when {
            redirectUri != null -> {
                authInFlight = false
                handleRedirectUri(redirectUri)
            }
            savedInstanceState != null -> {
                authInFlight = savedInstanceState.getBoolean(STATE_AUTH_IN_FLIGHT)
            }
            else -> startLoginRequest()
        }
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        outState.putString(EXTRA_BACKEND_BASE_URL, options.backendBaseUrl)
        outState.putString(EXTRA_CLIENT_NAME, options.clientName)
        outState.putString(EXTRA_RETURN_PATH, options.returnPath)
        outState.putString(EXTRA_LOGIN_PATH, options.loginPath)
        outState.putString(EXTRA_CALLBACK_PATH, options.loginCallbackPath)
        outState.putBoolean(STATE_AUTH_IN_FLIGHT, authInFlight)
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
        //
        // ÖNEMLİ: Bazı Android sürümlerinde/OEM'lerde, LinbikRedirectActivity'nin ilettiği
        // gerçek bir tamamlama intent'i teslim edilirken onResume() çağrısı onNewIntent()'ten
        // ÖNCE tetiklenebiliyor (dokümante edilen "onNewIntent önce, onResume sonra" sırası
        // garanti değil). Bu durumda authInFlight henüz false'a çekilmemiş olur ve geçerli bir
        // giriş burada yanlışlıkla iptal edilmiş sayılır. Kontrolü ana thread kuyruğunun sonuna
        // ertelemek, aynı anda işlenmekte olan bir onNewIntent()'e öncelik tanır.
        if (authInFlight) {
            Handler(Looper.getMainLooper()).post {
                if (authInFlight) finishCancelled()
            }
        }
    }

    private fun startLoginRequest() {
        val nameParam = options.clientName?.let { "&name=${encodeUrlParam(it)}" } ?: ""
        val returnParam = options.returnPath?.let { "&returnPath=${encodeUrlParam(it)}" } ?: ""
        val loginUrl = "${options.backendBaseUrl.trimEnd('/')}${options.loginPath}?_=1$nameParam$returnParam"

        scope.launch {
            try {
                val json = withContext(Dispatchers.IO) { httpClient.getJson(loginUrl) }
                handleLoginResponse(json)
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                Log.w(TAG, "Login request failed for $loginUrl", e)
                finishError(mapThrowableToMessage(e))
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
        persistOptions()
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

    /**
     * `authInFlight = true` olduğu andan itibaren (Custom Tabs açılmadan hemen önce) [options]'ı
     * kalıcı depoya yazar. Bu, [onCreate]'in redirect teslimatı sırasında Activity'nin tamamen
     * yeni bir örneği olarak (extra'sız, savedInstanceState=null) çağrılması durumunda son bilinen
     * options'ı geri okuyabilmesi içindir — bkz. [onCreate] içindeki açıklama.
     */
    private fun persistOptions() {
        prefs.edit()
            .putString(EXTRA_BACKEND_BASE_URL, options.backendBaseUrl)
            .putString(EXTRA_CLIENT_NAME, options.clientName)
            .putString(EXTRA_RETURN_PATH, options.returnPath)
            .putString(EXTRA_LOGIN_PATH, options.loginPath)
            .putString(EXTRA_CALLBACK_PATH, options.loginCallbackPath)
            .apply()
    }

    private fun loadPersistedOptions(): LinbikPasetoAuthOptions? {
        val backendBaseUrl = prefs.getString(EXTRA_BACKEND_BASE_URL, null) ?: return null
        return LinbikPasetoAuthOptions(
            backendBaseUrl = backendBaseUrl,
            clientName = prefs.getString(EXTRA_CLIENT_NAME, null),
            returnPath = prefs.getString(EXTRA_RETURN_PATH, null),
            loginPath = prefs.getString(EXTRA_LOGIN_PATH, null) ?: "/api/Linbik/login",
            loginCallbackPath = prefs.getString(EXTRA_CALLBACK_PATH, null) ?: "/api/Linbik/callback",
        )
    }

    private fun clearPersistedOptions() {
        prefs.edit().clear().apply()
    }

    private val prefs: android.content.SharedPreferences
        get() = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)

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
                val json = withContext(Dispatchers.IO) { httpClient.getJson(callbackUrl) }
                handleCallbackResponse(json)
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                Log.w(TAG, "Callback request failed", e)
                finishError(mapThrowableToMessage(e))
            }
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

    private fun mapThrowableToMessage(e: Throwable): String = when (e) {
        is java.net.UnknownHostException ->
            "Sunucu adresi bulunamadı. İnternet bağlantınızı veya backend adresini kontrol edin."
        is java.net.ConnectException ->
            "Sunucuya bağlanılamadı. Backend'in çalıştığından ve adresin (URL) doğru olduğundan emin olun."
        is java.net.SocketTimeoutException ->
            "Sunucu yanıt vermiyor (Zaman aşımı)."
        is IllegalStateException ->
            e.message ?: "Beklenmeyen bir sunucu hatası oluştu."
        else ->
            e.message ?: "Bir ağ hatası oluştu."
    }

    private fun finishSuccess(userId: String, userName: String, displayName: String, integrations: List<String>) {
        clearPersistedOptions()
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
        clearPersistedOptions()
        val result = Intent().apply {
            putExtra(EXTRA_RESULT_TYPE, RESULT_TYPE_ERROR)
            putExtra(EXTRA_ERROR_MESSAGE, message)
        }
        setResult(Activity.RESULT_OK, result)
        finish()
    }

    private fun finishCancelled() {
        clearPersistedOptions()
        setResult(Activity.RESULT_CANCELED)
        finish()
    }

    override fun onDestroy() {
        job.cancel()
        super.onDestroy()
    }

    companion object {
        private const val TAG = "LinbikAuth"
        private const val STATE_AUTH_IN_FLIGHT = "linbik.authInFlight"
        private const val PREFS_NAME = "linbik_pasetoauth_state"

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

        private fun Intent.toOptionsOrNull(): LinbikPasetoAuthOptions? {
            val backendBaseUrl = getStringExtra(EXTRA_BACKEND_BASE_URL) ?: return null
            return LinbikPasetoAuthOptions(
                backendBaseUrl = backendBaseUrl,
                clientName = getStringExtra(EXTRA_CLIENT_NAME),
                returnPath = getStringExtra(EXTRA_RETURN_PATH),
                loginPath = getStringExtra(EXTRA_LOGIN_PATH) ?: "/api/Linbik/login",
                loginCallbackPath = getStringExtra(EXTRA_CALLBACK_PATH) ?: "/api/Linbik/callback",
            )
        }

        private fun Bundle.toOptionsOrNull(): LinbikPasetoAuthOptions? {
            val backendBaseUrl = getString(EXTRA_BACKEND_BASE_URL) ?: return null
            return LinbikPasetoAuthOptions(
                backendBaseUrl = backendBaseUrl,
                clientName = getString(EXTRA_CLIENT_NAME),
                returnPath = getString(EXTRA_RETURN_PATH),
                loginPath = getString(EXTRA_LOGIN_PATH) ?: "/api/Linbik/login",
                loginCallbackPath = getString(EXTRA_CALLBACK_PATH) ?: "/api/Linbik/callback",
            )
        }

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
