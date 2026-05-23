namespace Linbik.PasetoAuthManager.Services;

/// <summary>
/// Reads (without signature/MAC verification) the claims of PASETO tokens issued by
/// <c>Linbik.PasetoAuthManager</c> for the current application's local cookie/session flow.
/// Mode-aware: handles both v4.public and v4.local tokens transparently using the
/// configured <see cref="Configuration.PasetoAuthOptions"/>.
/// <para>
/// Do NOT use this to inspect integration / service-to-service tokens issued by the
/// Linbik API — for those use <see cref="Linbik.Core.Services.Interfaces.IPasetoHelper"/>
/// directly, which is always v4.public.
/// </para>
/// </summary>
public interface ILocalPasetoTokenReader
{
    /// <summary>
    /// Parse the claims out of a locally-issued PASETO token. Returns an empty dictionary
    /// when the token is malformed, the mode is misconfigured, or parsing fails.
    /// </summary>
    /// <param name="token">PASETO token string (v4.public or v4.local depending on configured mode).</param>
    /// <returns>Claim key/value pairs.</returns>
    Dictionary<string, string> Read(string token);
}
