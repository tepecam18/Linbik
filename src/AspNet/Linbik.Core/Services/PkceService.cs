using Microsoft.AspNetCore.Http;

namespace Linbik.Core.Services;

public static class PkceService
{
    private const string PkceCookie = "pkce_verifier";
    private const int DefaultVerifierBytes = 32;
    private const int VerifierExpirationMinutes = 5;

    public static (string Verifier, string Challenge) Generate()
    {
        var verifier = NewVerifier(DefaultVerifierBytes);
        var challenge = ChallengeS256(verifier);
        return (verifier, challenge);
    }

    public static void SaveVerifier(HttpResponse response, string verifier)
    {
        response.Cookies.Append(PkceCookie, verifier, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddMinutes(VerifierExpirationMinutes)
        });
    }

    public static string? GetVerifier(HttpRequest request)
    {
        return request.Cookies.TryGetValue(PkceCookie, out var verifier) ? verifier : null;
    }

    public static void DeleteVerifier(HttpResponse response)
    {
        response.Cookies.Delete(PkceCookie, new CookieOptions { Path = "/" });
    }

    public static object BuildAuthorizeBody(HttpResponse response)
    {
        var (verifier, challenge) = Generate();
        SaveVerifier(response, verifier);

        return new
        {
            code_challenge = challenge
        };
    }

    public static bool VerifyChallengeMatches(string verifier, string expectedChallenge)
    {
        if (string.IsNullOrEmpty(verifier) || string.IsNullOrEmpty(expectedChallenge))
            return false;

        var actual = ChallengeS256(verifier);
        return FixedTimeEquals(actual, expectedChallenge);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;

        var ba = System.Text.Encoding.ASCII.GetBytes(a);
        var bb = System.Text.Encoding.ASCII.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static string NewVerifier(int bytes = DefaultVerifierBytes)
    {
        var randomBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(bytes);
        return Base64Url(randomBytes);
    }

    private static string ChallengeS256(string verifier)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    private static string Base64Url(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}