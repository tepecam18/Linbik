using Linbik.Core.Interfaces;
using Linbik.Server.Interfaces;

namespace AspNet.Repositories;

/// <summary>
/// Mock implementation of ILinbikServerRepository for testing purposes
/// In production, this should be replaced with a proper database implementation
/// </summary>
public class LinbikServerRepository : ILinbikServerRepository
{
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
        // Mock implementation - allow all IPs (should be implemented with database)
        return Task.FromResult(true);
    }
}
