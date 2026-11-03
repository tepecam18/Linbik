namespace Linbik.Core.Services.Interfaces;

/// <summary>
/// Abstraction for reading/writing Linbik credentials (Keyless Mode).
/// Supports .linbik/credentials.json file-based cache with thread-safe access.
/// </summary>
public interface ILinbikCredentialStore
{
    /// <summary>
    /// Try to load cached credentials. Returns null if no cache exists.
    /// </summary>
    Task<LinbikCredentials?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save credentials to the cache file.
    /// </summary>
    Task SaveAsync(LinbikCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete the cache file (e.g., on error).
    /// </summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if cached credentials exist.
    /// </summary>
    Task<bool> HasValidCredentialsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cached credentials for Keyless Mode provisioned services.
/// Stored in .linbik/credentials.json
/// </summary>
public sealed class LinbikCredentials
{
    public string ServiceId { get; set; } = string.Empty;
    /// <summary>Provisioned service audience for incoming PASETO tokens.</summary>
    public string PackageName { get; set; } = string.Empty;
    /// <summary>Base64 Ed25519 public key; private signing keys remain on Linbik.</summary>
    public string PublicKey { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? ClaimToken { get; set; }
    public string? ClaimUrl { get; set; }
    public bool IsClaimed { get; set; }
    public DateTime ProvisionedAt { get; set; }
}
