package com.linbik.pasetoauth

import android.app.Activity
import android.content.Intent
import android.os.Bundle

/**
 * Linbik'in Custom Tabs'ten `{applicationId}://oauth/callback?code=...` şemasıyla döndüğü
 * dış (exported) giriş noktası. Görünür bir UI'si yoktur; aldığı yönlendirmeyi zaten çalışmakta
 * olan [LinbikAuthActivity] örneğine iletip hemen kapanır.
 */
internal class LinbikRedirectActivity : Activity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        startActivity(
            Intent(this, LinbikAuthActivity::class.java).apply {
                data = intent.data
                addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP)
            },
        )
        finish()
    }
}
