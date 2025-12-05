using Linbik.Core.Interfaces;

namespace Linbik.Server.Interfaces;

public interface ILinbikServerRepository
{
    // === Authorization Code Flow ===

    /// <summary>
    /// Gets service by API key (for token exchange authentication)
    /// </summary>
    Task<ServiceData?> GetServiceByApiKeyAsync(string apiKey);

    /// <summary>
    /// Gets service by ID
    /// </summary>
    Task<ServiceData?> GetServiceByIdAsync(Guid serviceId);

    /// <summary>
    /// Validates and uses an authorization code (marks as used)
    /// </summary>
    Task<(bool isValid, AuthorizationCodeData? data)> ValidateAndUseAuthorizationCodeAsync(string code, Guid serviceId);

    /// <summary>
    /// Gets integration services that user has granted access to for a main service
    /// </summary>
    Task<List<ServiceData>> GetGrantedIntegrationServicesAsync(Guid userId, Guid mainServiceId);

    /// <summary>
    /// Gets user profile information
    /// </summary>
    Task<UserProfileData?> GetUserProfileAsync(Guid userId, Guid profileId);

    // === Refresh Token Management ===

    /// <summary>
    /// Creates a new refresh token
    /// </summary>
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
    Task<(bool isValid, RefreshTokenData? data)> ValidateRefreshTokenAsync(string token, Guid serviceId);

    /// <summary>
    /// Updates last used timestamp of refresh token
    /// </summary>
    Task<bool> UpdateRefreshTokenLastUsedAsync(string token);

    /// <summary>
    /// Revokes a refresh token
    /// </summary>
    Task<bool> RevokeRefreshTokenAsync(string token);

    // === IP Validation ===

    /// <summary>
    /// Validates if an IP address is allowed for a service
    /// </summary>
    Task<bool> IsIpAllowedAsync(Guid serviceId, string ipAddress);
}

/// <summary>
/// User profile data for token generation
/// </summary>
public class UserProfileData
{
    public Guid UserId { get; set; }
    public Guid ProfileId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
}

