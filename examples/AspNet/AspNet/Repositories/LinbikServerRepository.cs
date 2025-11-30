using Linbik.Core.Interfaces;
using Linbik.Server.Interfaces;
using Linbik.Server.Models;
using Linbik.Server.Responses;

namespace AspNet.Repositories;

/// <summary>
/// Mock implementation of ILinbikServerRepository for testing purposes
/// In production, this should be replaced with a proper database implementation
/// </summary>
public class LinbikServerRepository : ILinbikServerRepository
{
    #region Legacy Methods (Deprecated)

    [Obsolete("Use GetServiceByApiKeyAsync with authorization code flow instead")]
    public Task<AppLoginValidationResponse> AppLoginValidationsAsync(AppLoginModel request)
    {
        return Task.FromResult(new AppLoginValidationResponse
        {
            Success = true,
            Claims = new()
        });
    }

    #endregion

    #region Authorization Code Methods (Mock Implementation)

    public Task<ServiceData?> GetServiceByApiKeyAsync(string apiKey)
    {
        // Mock implementation - return null (should be implemented with database)
        return Task.FromResult<ServiceData?>(null);
    }

    public Task<ServiceData?> GetServiceByIdAsync(Guid serviceId)
    {
        // Mock implementation - return null (should be implemented with database)
        return Task.FromResult<ServiceData?>(null);
    }

    public Task<(bool isValid, AuthorizationCodeData? data)> ValidateAndUseAuthorizationCodeAsync(string code, Guid serviceId)
    {
        // Mock implementation - return invalid (should be implemented with database)
        return Task.FromResult<(bool, AuthorizationCodeData?)>((false, null));
    }

    public Task<List<ServiceData>> GetGrantedIntegrationServicesAsync(Guid userId, Guid mainServiceId)
    {
        // Mock implementation - return empty list (should be implemented with database)
        return Task.FromResult(new List<ServiceData>());
    }

    public Task<UserProfileData?> GetUserProfileAsync(Guid userId, Guid profileId)
    {
        // Mock implementation - return null (should be implemented with database)
        return Task.FromResult<UserProfileData?>(null);
    }

    public Task<string> CreateRefreshTokenAsync(
        Guid userId,
        Guid profileId,
        Guid serviceId,
        List<Guid> grantedIntegrationServiceIds,
        Guid authorizationCodeId,
        string? clientIp = null,
        int expirationDays = 30)
    {
        // Mock implementation - return random token (should be implemented with database)
        return Task.FromResult(Guid.NewGuid().ToString());
    }

    public Task<(bool isValid, RefreshTokenData? data)> ValidateRefreshTokenAsync(string token, Guid serviceId)
    {
        // Mock implementation - return invalid (should be implemented with database)
        return Task.FromResult<(bool, RefreshTokenData?)>((false, null));
    }

    public Task<bool> UpdateRefreshTokenLastUsedAsync(string token)
    {
        // Mock implementation - return false (should be implemented with database)
        return Task.FromResult(false);
    }

    public Task<bool> RevokeRefreshTokenAsync(string token)
    {
        // Mock implementation - return false (should be implemented with database)
        return Task.FromResult(false);
    }

    public Task<bool> IsIpAllowedAsync(Guid serviceId, string ipAddress)
    {
        // Mock implementation - return true (allow all IPs for testing)
        return Task.FromResult(true);
    }

    #endregion
}
