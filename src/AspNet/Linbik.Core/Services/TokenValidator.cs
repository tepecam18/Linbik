using Linbik.Core.Configuration;
using Linbik.Core.Interfaces;
using Linbik.Core.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace Linbik.Core.Services;

public class TokenValidator(IOptions<LinbikOptions> options, ILogger<TokenValidator> logger) : ITokenValidator
{
    private const string AppIdClaimType = "appId";
    private const string CodeClaimType = "code";

    public Task<TokenValidatorResponse> ValidateToken(string token, string verifier, bool pkceEnabled)
    {
        logger.LogDebug("Starting token validation. PKCE enabled: {PkceEnabled}", pkceEnabled);

        // JWT'yi doğrula
        var tokenHandler = new JwtSecurityTokenHandler();

        using var rsa = RSA.Create();

        string pemPublicKey = $"-----BEGIN PUBLIC KEY-----\n{options.Value.PublicKey}\n-----END PUBLIC KEY-----";
        rsa.ImportFromPem(pemPublicKey.ToCharArray());

        // Tokenı doğrulamak için ayarlar oluşturun
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false, // İstemciyi doğrulamıyoruz
            ValidateAudience = false, // İstemciyi doğrulamıyoruz
            ValidateLifetime = true, // Tokenın süresini doğrula
            ValidateIssuerSigningKey = true, // Sunucu tarafından kullanılan anahtarı doğrula
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new RsaSecurityKey(rsa),
        };

        try
        {
            var claimsPrincipal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var validatedToken);

            var appId = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == AppIdClaimType)?.Value;
            var code = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == CodeClaimType)?.Value ?? "";

            // AppId null check
            if (string.IsNullOrEmpty(appId))
            {
                logger.LogWarning("AppId claim not found in token");
                return Task.FromResult(new TokenValidatorResponse
                {
                    Success = false,
                    Message = "AppId claim not found in token."
                });
            }

            logger.LogInformation("Token validated successfully. AppId: {AppId}", appId);

            if (!options.Value.AppIds.Contains(appId) && !options.Value.AllowAllApp)
            {
                logger.LogWarning("AppId {AppId} is not allowed", appId);
                return Task.FromResult(new TokenValidatorResponse
                {
                    Success = false,
                    Message = $"AppId {appId} is not allowed."
                });
            }

            var pkceStatus = PkceService.VerifyChallengeMatches(verifier, code);

            if (!pkceStatus && pkceEnabled)
            {
                logger.LogWarning("PKCE verification failed for AppId: {AppId}", appId);
                return Task.FromResult(new TokenValidatorResponse
                {
                    Success = false,
                    Message = $"The login process is invalid. Please try again."
                });
            }

            logger.LogInformation("Token validation completed successfully for AppId: {AppId}", appId);
            return Task.FromResult(new TokenValidatorResponse
            {
                Success = true,
                Claims = claimsPrincipal.Claims,
            });
        }
        catch (SecurityTokenValidationException ex)
        {
            logger.LogError(ex, "Token validation failed: {Message}", ex.Message);
            return Task.FromResult(new TokenValidatorResponse
            {
                Success = false,
                Message = $"Token validation failed: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred during token validation");
            return Task.FromResult(new TokenValidatorResponse
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}"
            });
        }
    }
}
