using Linbik.Core.Interfaces;
using Linbik.Core.Responses;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Interfaces;
using Linbik.JwtAuthManager.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Linbik.JwtAuthManager.Services;

/// <summary>
/// In-memory implementation of ILinbikRepository (for testing purposes only)
/// Production implementations should use a proper database
/// </summary>
public class InMemoryLinbikRepository : ILinbikRepository
{
    private readonly ConcurrentDictionary<string, TokenModel> _tokens = new();
    private readonly IOptions<JwtAuthOptions> _jwtOptions;

    public InMemoryLinbikRepository(IOptions<JwtAuthOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions;
    }

    #region Legacy Methods (Deprecated)

    [Obsolete("Use IRefreshTokenService instead")]
    public Task<(string refreshToken, bool success)> CreateRefresToken(Guid userGuid, string name)
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        var refreshToken = Convert.ToBase64String(randomNumber);

        var token = new TokenModel
        {
            RefreshToken = refreshToken,
            Expiration = DateTime.UtcNow.AddDays(_jwtOptions.Value.RefreshTokenExpiration),
            UserGuid = userGuid,
            Name = name
        };

        _tokens.TryAdd(refreshToken, token);

        return Task.FromResult((refreshToken, true));
    }

    [Obsolete("Use proper OAuth 2.0 flow")]
    public Task LoggedInUser(Guid userGuid, string name)
    {
        return Task.CompletedTask;
    }

    [Obsolete("Use IRefreshTokenService.ValidateRefreshTokenAsync instead")]
    public Task<TokenValidatorResponse> UseRefresToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(new TokenValidatorResponse
            {
                Success = false,
                Message = "Token is null or empty."
            });
        }

        if (_tokens.TryGetValue(token, out var tokenData))
        {
            if (tokenData.Expiration < DateTime.UtcNow)
            {
                _tokens.TryRemove(token, out _);
                return Task.FromResult(new TokenValidatorResponse
                {
                    Success = false,
                    Message = "Token has expired."
                });
            }

            return Task.FromResult(new TokenValidatorResponse
            {
                Success = true,
                UserGuid = tokenData.UserGuid,
                Name = tokenData.Name,
            });
        }

        return Task.FromResult(new TokenValidatorResponse
        {
            Success = false,
            Message = "Invalid refresh token."
        });
    }

    #endregion

    #region OAuth 2.0 Methods (Stub Implementation)

    /// <summary>
    /// Gets service by API key (stub - should be implemented in database repository)
    /// </summary>
    public Task<ServiceData?> GetServiceByApiKeyAsync(string apiKey)
    {
        // In-memory implementation - return null (should be implemented in real repository)
        return Task.FromResult<ServiceData?>(null);
    }

    /// <summary>
    /// Validates authorization code (stub - should be implemented in database repository)
    /// </summary>
    public Task<(bool isValid, AuthorizationCodeData? data)> ValidateAuthorizationCodeAsync(string code, Guid serviceId)
    {
        // In-memory implementation - return invalid (should be implemented in real repository)
        return Task.FromResult<(bool, AuthorizationCodeData?)>((false, null));
    }

    /// <summary>
    /// Creates refresh token (stub - should be implemented in database repository)
    /// </summary>
    public Task<string> CreateRefreshTokenAsync(
        Guid userId,
        Guid profileId,
        Guid serviceId,
        List<Guid> grantedIntegrationServiceIds,
        Guid authorizationCodeId,
        string? clientIp = null)
    {
        // In-memory implementation - generate random token (should be implemented in real repository)
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Task.FromResult(Convert.ToBase64String(randomNumber));
    }

    /// <summary>
    /// Validates refresh token (stub - should be implemented in database repository)
    /// </summary>
    public Task<(bool isValid, RefreshTokenData? data)> ValidateRefreshTokenAsync(string token, Guid serviceId)
    {
        // In-memory implementation - return invalid (should be implemented in real repository)
        return Task.FromResult<(bool, RefreshTokenData?)>((false, null));
    }

    #endregion
}
