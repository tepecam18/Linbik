namespace Linbik.Core.Services.Interfaces;

/// <summary>
/// PASETO mode selection. Determines whether tokens are signed with an asymmetric
/// Ed25519 key pair (v4.public) or encrypted with a symmetric shared key (v4.local).
/// </summary>
public enum PasetoMode
{
    /// <summary>
    /// PASETO v4.public — Ed25519 asymmetric signing. Recommended when token consumers
    /// are separate processes/services that must verify tokens without holding the
    /// private signing key (only the public key is distributed).
    /// </summary>
    Public = 0,

    /// <summary>
    /// PASETO v4.local — XChaCha20-Poly1305 symmetric encryption. Recommended for
    /// single-process cookie/session scenarios where the same trust boundary both
    /// issues and validates tokens. Payload is encrypted (not just signed).
    /// </summary>
    Local = 1,
}
