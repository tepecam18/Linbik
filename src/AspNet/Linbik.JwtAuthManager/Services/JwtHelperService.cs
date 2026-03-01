using Linbik.Core.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Linbik.JwtAuthManager.Services;

/// <summary>
/// JWT helper for multi-service authentication with RSA-256 signing
/// </summary>
public sealed class JwtHelperService : IJwtHelper
{
    /// <summary>
    /// Creates a JWT token signed with a service's RSA private key
    /// </summary>
    public Task<string> CreateTokenAsync(Claim[] claims, string privateKey, string audience, int expirationMinutes = 60)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        using var rsa = RSA.Create();
        var privateKeyBytes = Convert.FromBase64String(privateKey);
        rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
        CryptographicOperations.ZeroMemory(privateKeyBytes);

        var rsaSecurityKey = new RsaSecurityKey(rsa);
        var credentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Issuer = Core.LinbikDefaults.Issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Task.FromResult(tokenHandler.WriteToken(token));
    }

    /// <summary>
    /// Validates a JWT token using a service's RSA public key
    /// </summary>
    public Task<bool> ValidateTokenAsync(string token, string publicKey, string expectedAudience, string expectedIssuer = Core.LinbikDefaults.Issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedAudience);

        try
        {
            using var rsa = RSA.Create();
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
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Extracts claims from a JWT token without validation
    /// </summary>
    public Dictionary<string, string> GetTokenClaims(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Dictionary<string, string> claims = [];
        foreach (var claim in jwtToken.Claims)
        {
            claims[claim.Type] = claim.Value;
        }

        return claims;
    }
}
