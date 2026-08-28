package com.linbik.pasetoauth.sample

import android.os.Bundle
import androidx.activity.result.ActivityResultLauncher
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.linbik.pasetoauth.LinbikAuthResult
import com.linbik.pasetoauth.LinbikPasetoAuthClient
import com.linbik.pasetoauth.LinbikPasetoAuthOptions
import com.linbik.pasetoauth.sample.databinding.ActivityMainBinding
import kotlinx.coroutines.launch

/**
 * Linbik.PasetoAuthManager.Android kütüphanesinin kullanımını gösteren minimal örnek.
 * Backend olarak Linbik/examples/AspNet/AspNet projesini (veya PasetoAuthManager kullanan
 * kendi ASP.NET backend'inizi) kullanabilirsiniz — bkz. README → "Backend Yapılandırması".
 */
class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding
    private val authClient = LinbikPasetoAuthClient()
    private lateinit var launcher: ActivityResultLauncher<LinbikPasetoAuthOptions>
    private var lastOptions: LinbikPasetoAuthOptions? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // registerForActivityResult, Activity STARTED olmadan (yani burada, onCreate'te)
        // çağrılmalıdır — buton tıklamasında çağrılırsa IllegalStateException fırlatılır.
        launcher = authClient.registerLauncher(this) { result -> handleResult(result) }

        binding.signInButton.setOnClickListener { startSignIn() }
        binding.signOutButton.setOnClickListener { signOut() }
    }

    private fun startSignIn() {
        val backendUrl = binding.backendUrlInput.text?.toString()?.trim().orEmpty()
        val clientName = binding.clientNameInput.text?.toString()?.trim().orEmpty()
        if (backendUrl.isEmpty()) {
            binding.statusText.text = "Lütfen bir backend adresi girin."
            return
        }

        val options = LinbikPasetoAuthOptions(
            backendBaseUrl = backendUrl,
            clientName = clientName.ifEmpty { null },
        )
        lastOptions = options
        binding.statusText.text = "Linbik'e yönlendiriliyor…"
        launcher.launch(options)
    }

    private fun handleResult(result: LinbikAuthResult) {
        when (result) {
            is LinbikAuthResult.Success -> {
                binding.statusText.text = buildString {
                    appendLine("✅ Giriş başarılı")
                    appendLine("Kullanıcı: ${result.displayName} (@${result.userName})")
                    appendLine("userId: ${result.userId}")
                    if (result.integrations.isNotEmpty()) {
                        appendLine("Entegrasyonlar: ${result.integrations.joinToString()}")
                    }
                }
                binding.signOutButton.visibility = android.view.View.VISIBLE
            }
            is LinbikAuthResult.Error -> {
                binding.statusText.text = "❌ Hata: ${result.message}"
            }
            LinbikAuthResult.Cancelled -> {
                binding.statusText.text = "Giriş iptal edildi."
            }
        }
    }

    private fun signOut() {
        val options = lastOptions ?: return
        lifecycleScope.launch {
            val success = authClient.signOut(options)
            binding.statusText.text = if (success) "Çıkış yapıldı." else "Çıkış sırasında bir sorun oluştu."
            binding.signOutButton.visibility = android.view.View.GONE
        }
    }
}
