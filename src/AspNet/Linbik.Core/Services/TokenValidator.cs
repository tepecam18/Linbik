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
    private const string CodeClaimType = "code";

    public Task<TokenValidatorResponse> ValidateToken(string token, string verifier, bool pkceEnabled)
    {
        logger.LogDebug("Starting token validation. PKCE enabled: {PkceEnabled}", pkceEnabled);

        // JWT'yi doğrula
        var tokenHandler = new JwtSecurityTokenHandler();

        // RSA private key ile RSA nesnesi oluştur
        using var rsa = RSA.Create();
        var bytes = Convert.FromBase64String(options.Value.PublicKey);
        rsa.ImportSubjectPublicKeyInfo(bytes, out _);

        // Tokenı doğrulamak için ayarlar oluşturun
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "linbik",
            ValidateAudience = !options.Value.AllowAllApp,
            ValidAudiences = options.Value.AppIds,
            ValidateLifetime = true, // Tokenın süresini doğrula
            ValidateIssuerSigningKey = true, // Sunucu tarafından kullanılan anahtarı doğrula
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new RsaSecurityKey(rsa)//fix to cache problem(problem from Microsoft.IdentityModel.Tokens)
            {
                CryptoProviderFactory = new CryptoProviderFactory()
                {
                    CacheSignatureProviders = false
                }
            },
        };

        try
        {
            var claimsPrincipal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var validatedToken);

            var code = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == CodeClaimType)?.Value ?? "";


            var pkceStatus = PkceService.VerifyChallengeMatches(verifier, code);

            if (!pkceStatus && pkceEnabled)
            {
                logger.LogWarning("PKCE verification failed");
                return Task.FromResult(new TokenValidatorResponse
                {
                    Success = false,
                    Message = $"The login process is invalid. Please try again."
                });
            }

            logger.LogInformation("Token validation completed successfully");
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
