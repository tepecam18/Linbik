using System.Security.Cryptography;
using System.Text;

namespace AspNet.Helpers;

/// <summary>
/// PKCE (Proof Key for Code Exchange) helper methods
/// </summary>
public static class PkceHelper
{
    /// <summary>
    /// Generates a cryptographically random code verifier
    /// </summary>
    /// <param name="length">Length of the verifier (43-128 characters)</param>
    /// <returns>Base64URL encoded code verifier</returns>
    public static string GenerateCodeVerifier(int length = 128)
    {
        if (length < 43 || length > 128)
            throw new ArgumentException("Code verifier length must be between 43 and 128 characters", nameof(length));

        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Generates a code challenge from a code verifier using SHA256
    /// </summary>
    /// <param name="codeVerifier">Code verifier string</param>
    /// <returns>Base64URL encoded SHA256 hash of the verifier</returns>
    public static string GenerateCodeChallenge(string codeVerifier)
    {
        if (string.IsNullOrEmpty(codeVerifier))
            throw new ArgumentException("Code verifier cannot be null or empty", nameof(codeVerifier));

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(codeVerifier);
        var hash = sha256.ComputeHash(bytes);
        
        return Base64UrlEncode(hash);
    }

    /// <summary>
    /// Validates that a code challenge matches a code verifier
    /// </summary>
    /// <param name="codeVerifier">Original code verifier</param>
    /// <param name="codeChallenge">Code challenge to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateCodeChallenge(string codeVerifier, string codeChallenge)
    {
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(codeChallenge))
            return false;

        var computedChallenge = GenerateCodeChallenge(codeVerifier);
        return computedChallenge.Equals(codeChallenge, StringComparison.Ordinal);
    }

    /// <summary>
    /// Encodes bytes to Base64URL format (RFC 4648)
    /// </summary>
    private static string Base64UrlEncode(byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        
        // Convert to Base64URL (RFC 4648 Section 5)
        return base64
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    /// <summary>
    /// Decodes Base64URL string to bytes
    /// </summary>
    public static byte[] Base64UrlDecode(string base64Url)
    {
        if (string.IsNullOrEmpty(base64Url))
            throw new ArgumentException("Input cannot be null or empty", nameof(base64Url));

        // Convert from Base64URL to standard Base64
        var base64 = base64Url
            .Replace("-", "+")
            .Replace("_", "/");

        // Add padding if necessary
        var padding = (4 - base64.Length % 4) % 4;
        base64 += new string('=', padding);

        return Convert.FromBase64String(base64);
    }
}
