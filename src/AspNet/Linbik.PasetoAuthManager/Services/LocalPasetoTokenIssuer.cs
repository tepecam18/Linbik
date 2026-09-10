using Linbik.Core.Services.Interfaces;
using Linbik.PasetoAuthManager.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Linbik.PasetoAuthManager.Services;

/// <summary>
/// Cookie tabanlı kimlik doğrulama için PASETO v4.public (Ed25519) access token üretir.
/// Servisler arası (Application) token üretiminde kullanılmaz; yalnızca <c>authToken</c> cookie'si içindir.
/// </summary>
internal static class LocalPasetoTokenIssuer
{
    // Standard JWT/PASETO registered claim names (RFC 7519)
    private const string ClaimSub = "sub";
    private const string ClaimPreferredUsername = "preferred_username";
    private const string ClaimName = "name";

    /// <summary>
    /// Yerel cookie access token üretir. v4.public modunda Ed25519 ile imzalanır,
    /// v4.local modunda simetrik shared key ile şifrelenir.
    /// Gerekli anahtar eksik veya geçersizse null döner ve hatayı loglar.
    /// </summary>
    internal static async Task<string?> CreateAsync(
        PasetoAuthOptions options,
        Core.Models.LinbikTokenResponse tokenResponse,
        DateTime accessTokenExpiry,
        IPasetoHelper pasetoHelper,
        ILogger logger)
    {
        Claim[] claims =
        [
            new(ClaimSub, tokenResponse.UserId.ToString()),
            new(ClaimPreferredUsername, tokenResponse.Username),
            new(ClaimName, tokenResponse.DisplayName ?? tokenResponse.Username),
        ];

        // Compute expiration minutes from expiry timestamp (clamped to a sane minimum)
        var expirationMinutes = Math.Max(1, (int)Math.Ceiling((accessTokenExpiry - DateTime.UtcNow).TotalMinutes));

        try
        {
            if (options.Mode == PasetoMode.Local)
            {
                if (string.IsNullOrEmpty(options.SharedKeyBase64))
                {
                    logger.LogError(
                        "SharedKeyBase64 is not configured in PasetoAuthOptions (Mode=Local). " +
                        "Please set 'Linbik:PasetoAuth:SharedKeyBase64' in appsettings.json. " +
                        "Generate a key with IPasetoHelper.GenerateSymmetricKey().");
                    return null;
                }

                return await pasetoHelper.CreateLocalTokenAsync(
                    claims,
                    options.SharedKeyBase64,
                    options.Audience,
                    expirationMinutes,
                    options.KeyId);
            }

            if (string.IsNullOrEmpty(options.PrivateKeyBase64))
            {
                logger.LogError(
                    "PrivateKeyBase64 is not configured in PasetoAuthOptions (Mode=Public). " +
                    "Please set 'Linbik:PasetoAuth:PrivateKeyBase64' in appsettings.json. " +
                    "Generate a key pair with IPasetoHelper.GenerateKeyPair().");
                return null;
            }

            return await pasetoHelper.CreateTokenAsync(
                claims,
                options.PrivateKeyBase64,
                options.Audience,
                expirationMinutes,
                options.KeyId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create local PASETO access token.");
            return null;
        }
    }
}
