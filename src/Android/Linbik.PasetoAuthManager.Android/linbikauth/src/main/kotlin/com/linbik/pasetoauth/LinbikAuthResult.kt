package com.linbik.pasetoauth

/** [LinbikPasetoAuthClient.signIn] akışının sonucu. */
sealed class LinbikAuthResult {
    data class Success(
        val userId: String,
        val userName: String,
        val displayName: String,
        val integrations: List<String>,
    ) : LinbikAuthResult()

    data class Error(val message: String) : LinbikAuthResult()

    /** Kullanıcı giriş ekranını geri tuşu/kapatarak iptal etti. */
    data object Cancelled : LinbikAuthResult()
}
