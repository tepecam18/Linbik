using Linbik.Core.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Linbik.JwtAuthManager.Services;

/// <summary>
/// JWT helper for multi-service authentication with RSA-256 signing
/// </summary>
public class JwtHelperService : IJwtHelper
{
    /// <summary>
    /// Creates a JWT token signed with a service's RSA private key
    /// </summary>
    public async Task<string> CreateTokenAsync(Claim[] claims, string privateKey, string audience, int expirationMinutes = 60)
    {
        return await Task.Run(() =>
        {
            // Import RSA private key from Base64-encoded PKCS#8 format
            var rsa = RSA.Create();
            var privateKeyBytes = Convert.FromBase64String(privateKey);
            rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);

            var rsaSecurityKey = new RsaSecurityKey(rsa);
            var credentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                NotBefore = DateTime.UtcNow.AddMinutes(-1), // Allow 1 min clock skew
                Issuer = "linbik",
                Audience = audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        });
    }

    /// <summary>
    /// Validates a JWT token using a service's RSA public key
    /// </summary>
    public async Task<bool> ValidateTokenAsync(string token, string publicKey, string expectedAudience, string expectedIssuer = "linbik")
    {
        return await Task.Run(() =>
        {
            try
            {
                var rsa = RSA.Create();
                var publicKeyBytes = Convert.FromBase64String(publicKey);
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

                var rsaSecurityKey = new RsaSecurityKey(rsa);

                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = rsaSecurityKey,
                    ValidateIssuer = true,
                    ValidIssuer = expectedIssuer,
                    ValidateAudience = true,
                    ValidAudience = expectedAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                tokenHandler.ValidateToken(token, validationParameters, out _);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Extracts claims from a JWT token without validation
    /// </summary>
    public Dictionary<string, string> GetTokenClaims(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var claims = new Dictionary<string, string>();
        foreach (var claim in jwtToken.Claims)
        {
            claims[claim.Type] = claim.Value;
        }

        return claims;
    }
}
