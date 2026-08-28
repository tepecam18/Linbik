package com.linbik.pasetoauth

import android.app.Activity
import android.content.Context
import android.content.Intent
import androidx.activity.ComponentActivity
import androidx.activity.result.ActivityResultCaller
import androidx.activity.result.ActivityResultLauncher
import androidx.activity.result.contract.ActivityResultContract
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request

/**
 * Linbik.PasetoAuthManager tabanlı backend'inize karşı "Linbik ile Giriş Yap" akışını
 * başlatmak için kullanılan tek giriş noktası.
 *
 * ÖNEMLİ: `registerLauncher` çağrısı, [ActivityResultLauncher] API'sinin gereği olarak
 * Activity/Fragment henüz STARTED durumuna geçmeden (yani `onCreate` içinde) yapılmalıdır.
 * Buton tıklaması gibi daha sonraki bir olayda çağrılırsa `IllegalStateException` alırsınız.
 * Asıl seçenekler (`LinbikPasetoAuthOptions`) kayıt anında değil, `launch(options)` çağrısında
 * verilir — böylece backend adresi gibi bilgileri kullanıcıdan çalışma zamanında alabilirsiniz.
 *
 * ```kotlin
 * class MyActivity : ComponentActivity() {
 *     private val linbikAuth = LinbikPasetoAuthClient()
 *     private lateinit var launcher: ActivityResultLauncher<LinbikPasetoAuthOptions>
 *
 *     override fun onCreate(savedInstanceState: Bundle?) {
 *         super.onCreate(savedInstanceState)
 *         launcher = linbikAuth.registerLauncher(this) { result ->
 *             when (result) {
 *                 is LinbikAuthResult.Success -> ...
 *                 is LinbikAuthResult.Error -> ...
 *                 LinbikAuthResult.Cancelled -> ...
 *             }
 *         }
 *         signInButton.setOnClickListener {
 *             launcher.launch(LinbikPasetoAuthOptions(backendBaseUrl = "https://10.0.2.2:7020", clientName = "Mobile"))
 *         }
 *     }
 * }
 * ```
 */
class LinbikPasetoAuthClient {

    /** Bir [ComponentActivity]/Fragment yaşam döngüsüne bağlı bir launcher kaydeder (önerilen kullanım). */
    fun registerLauncher(
        caller: ActivityResultCaller,
        onResult: (LinbikAuthResult) -> Unit,
    ): ActivityResultLauncher<LinbikPasetoAuthOptions> = caller.registerForActivityResult(SignInContract(), onResult)

    /**
     * Sunucu tarafındaki oturumu (cookie) sonlandırır. WebView tabanlı çıkış için Activity
     * gerekmez — mevcut oturum çerezleriyle backend'in logout endpoint'ine istek atar.
     */
    suspend fun signOut(options: LinbikPasetoAuthOptions): Boolean = withContext(Dispatchers.IO) {
        try {
            val client = OkHttpClient.Builder().cookieJar(LinbikWebViewCookieJar()).build()
            val url = options.backendBaseUrl.trimEnd('/') + options.logoutPath
            client.newCall(Request.Builder().url(url).build()).execute().use { it.isSuccessful }
        } catch (e: Exception) {
            false
        }
    }

    private class SignInContract : ActivityResultContract<LinbikPasetoAuthOptions, LinbikAuthResult>() {
        override fun createIntent(context: Context, input: LinbikPasetoAuthOptions): Intent =
            LinbikAuthActivity.createIntent(context, input)

        override fun parseResult(resultCode: Int, intent: Intent?): LinbikAuthResult {
            if (resultCode != Activity.RESULT_OK || intent == null) return LinbikAuthResult.Cancelled
            return LinbikAuthActivity.parseResult(intent)
        }
    }

    companion object {
        /** Uygulamanızın kendi OkHttp istemcisinin Linbik oturum çerezlerini paylaşması için. */
        fun cookieJar(): okhttp3.CookieJar = LinbikWebViewCookieJar()
    }
}
