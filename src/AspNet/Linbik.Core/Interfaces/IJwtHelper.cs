using System.Security.Claims;

namespace Linbik.Core.Interfaces;

/// <summary>
/// JWT token generation and validation helper for multi-service authentication
/// </summary>
public interface IJwtHelper
{
    /// <summary>
    /// Creates a JWT token signed with a service's private key
    /// </summary>
    /// <param name="claims">Claims to include in the token</param>
    /// <param name="privateKey">Base64-encoded RSA private key (PKCS#8 format)</param>
    /// <param name="audience">Service ID that will receive this token</param>
    /// <param name="expirationMinutes">Token expiration time in minutes (default: 60)</param>
    /// <returns>JWT token string</returns>
    Task<string> CreateTokenAsync(Claim[] claims, string privateKey, string audience, int expirationMinutes = 60);

    /// <summary>
    /// Validates a JWT token using a service's public key
    /// </summary>
    /// <param name="token">JWT token to validate</param>
    /// <param name="publicKey">Base64-encoded RSA public key (X.509 SPKI format)</param>
    /// <param name="expectedAudience">Expected audience claim value</param>
    /// <param name="expectedIssuer">Expected issuer claim value (default: "linbik")</param>
    /// <returns>True if token is valid, false otherwise</returns>
    Task<bool> ValidateTokenAsync(string token, string publicKey, string expectedAudience, string expectedIssuer = "linbik");

    /// <summary>
    /// Extracts claims from a JWT token without validation
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>Dictionary of claims</returns>
    Dictionary<string, string> GetTokenClaims(string token);
}
