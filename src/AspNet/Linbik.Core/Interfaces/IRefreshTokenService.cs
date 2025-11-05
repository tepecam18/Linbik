namespace Linbik.Core.Interfaces;

/// <summary>
/// Service for managing OAuth 2.0 refresh tokens
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Creates a new refresh token
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="profileId">Profile ID</param>
    /// <param name="serviceId">Service ID</param>
    /// <param name="grantedIntegrationServiceIds">List of granted integration service IDs</param>
    /// <param name="authorizationCodeId">Related authorization code ID</param>
    /// <param name="clientIp">Client IP address</param>
    /// <param name="expirationDays">Token expiration in days (default: 30)</param>
    /// <returns>Generated refresh token string</returns>
    Task<string> CreateRefreshTokenAsync(
        Guid userId,
        Guid profileId,
        Guid serviceId,
        List<Guid> grantedIntegrationServiceIds,
        Guid authorizationCodeId,
        string? clientIp = null,
        int expirationDays = 30);

    /// <summary>
    /// Validates a refresh token and returns its data
    /// </summary>
    /// <param name="token">Refresh token</param>
    /// <param name="serviceId">Service attempting to use the token</param>
    /// <returns>Tuple containing validation result and token data (if valid)</returns>
    Task<(bool isValid, RefreshTokenData? data)> ValidateRefreshTokenAsync(string token, Guid serviceId);

    /// <summary>
    /// Revokes a refresh token
    /// </summary>
    /// <param name="token">Refresh token to revoke</param>
    /// <returns>True if revoked successfully, false if not found</returns>
    Task<bool> RevokeRefreshTokenAsync(string token);

    /// <summary>
    /// Revokes all refresh tokens for a user and service combination
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="serviceId">Service ID</param>
    /// <returns>Number of tokens revoked</returns>
    Task<int> RevokeAllUserServiceTokensAsync(Guid userId, Guid serviceId);

    /// <summary>
    /// Updates the last used timestamp of a refresh token
    /// </summary>
    /// <param name="token">Refresh token</param>
    /// <returns>True if updated successfully, false if not found</returns>
    Task<bool> UpdateLastUsedAsync(string token);
}

/// <summary>
/// Refresh token data model
/// </summary>
public class RefreshTokenData
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid ServiceId { get; set; }
    public List<Guid> GrantedIntegrationServiceIds { get; set; } = new();
    public Guid AuthorizationCodeId { get; set; }
    public string? ClientIp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
}
