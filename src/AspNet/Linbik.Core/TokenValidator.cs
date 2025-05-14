using Linbik.Core.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace Linbik.Core;

public class TokenValidator(IOptions<LinbikOptions> options) : ITokenValidator
{
    public async Task<TokenValidatorResponse> ValidateToken(string token)
    {
        // JWT'yi doğrula
        var tokenHandler = new JwtSecurityTokenHandler();

        var rsa = RSA.Create();

        string pemPublicKey = $"-----BEGIN PUBLIC KEY-----\n{options.Value.publicKey}\n-----END PUBLIC KEY-----";
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

            var userGuidStr = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            var userGuid = Guid.Parse(userGuidStr);
            var appId = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == "app")?.Value;
            var name = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == "name")?.Value;

            if (options.Value.appIds.Contains(appId) || options.Value.allowAllApp)
                return new()
                {
                    Success = false,
                    Message = $"AppId {appId} is not allowed"
                };

            return new TokenValidatorResponse
            {
                Success = true,
                UserGuid = userGuid,
                AppId = appId,
                Name = name,
            };
        }
        catch (SecurityTokenValidationException ex)
        {
            Console.WriteLine($"Token validation failed: {ex.Message}");
            return new()
            {
                Success = false,
                Message = $"Token validation failed: {ex.Message}"
            };
            //return new BaseResponse<GetLoginResponse>("token_not_valid", "Token Doğrulanamadı");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return new()
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}"
            };
        }
    }
}
