namespace Linbik.JwtAuthManager.Services;

/// <summary>
/// Reads (without signature verification) the claims of JWT tokens issued by
/// <c>Linbik.JwtAuthManager</c> for the current application's local cookie/session flow.
/// </summary>
public interface ILocalJwtTokenReader
{
    /// <summary>
    /// Parse the claims out of a locally-issued JWT. Returns an empty dictionary when the
    /// token is malformed or parsing fails.
    /// </summary>
    /// <param name="token">JWT compact serialization string.</param>
    /// <returns>Claim key/value pairs (last-write-wins on duplicate claim types).</returns>
    Dictionary<string, string> Read(string token);
}
