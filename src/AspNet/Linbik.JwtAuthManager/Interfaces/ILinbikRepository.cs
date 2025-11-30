using Linbik.Core.Responses;
using Linbik.Core.Interfaces;

namespace Linbik.JwtAuthManager.Interfaces;

public interface ILinbikRepository
{
    /// <summary>
    /// Legacy: Creates a refresh token (deprecated - use IRefreshTokenService)
    /// </summary>
    [Obsolete("Use IRefreshTokenService.CreateRefreshTokenAsync instead")]
    Task<(string refreshToken, bool success)> CreateRefresToken(Guid userGuid, string name);

    /// <summary>
    /// Legacy: Uses a refresh token (deprecated - use IRefreshTokenService)
    /// </summary>
    [Obsolete("Use IRefreshTokenService.ValidateRefreshTokenAsync instead")]
    Task<TokenValidatorResponse> UseRefresToken(string token);

    /// <summary>
    /// Legacy: Logs user login (deprecated)
    /// </summary>
    [Obsolete("Use proper authorization flow")]
    Task LoggedInUser(Guid userGuid, string name);

    // === New Authorization Methods ===
    // Implementations should delegate to IRefreshTokenService, IServiceRepository, etc.
    // This interface is kept for backward compatibility only

    /// <summary>
    /// Gets service by API key (delegates to IServiceRepository)
    /// </summary>
    Task<ServiceData?> GetServiceByApiKeyAsync(string apiKey);

    /// <summary>
    /// Validates authorization code (delegates to IAuthorizationCodeService)
    /// </summary>
    Task<(bool isValid, AuthorizationCodeData? data)> ValidateAuthorizationCodeAsync(string code, Guid serviceId);

    /// <summary>
    /// Creates refresh token (delegates to IRefreshTokenService)
    /// </summary>
    Task<string> CreateRefreshTokenAsync(
        Guid userId,
        Guid profileId,
        Guid serviceId,
        List<Guid> grantedIntegrationServiceIds,
        Guid authorizationCodeId,
        string? clientIp = null);

    /// <summary>
    /// Validates refresh token (delegates to IRefreshTokenService)
    /// </summary>
    Task<(bool isValid, RefreshTokenData? data)> ValidateRefreshTokenAsync(string token, Guid serviceId);
}
