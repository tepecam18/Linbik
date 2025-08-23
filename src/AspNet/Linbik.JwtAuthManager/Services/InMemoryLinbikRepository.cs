using Linbik.Core.Responses;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Interfaces;
using Linbik.JwtAuthManager.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Linbik.JwtAuthManager.Services;

public class InMemoryLinbikRepository : ILinbikRepository
{
    private readonly ConcurrentDictionary<string, TokenModel> _tokens = new();
    private readonly IOptions<JwtAuthOptions> _jwtOptions;

    public InMemoryLinbikRepository(IOptions<JwtAuthOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions;
    }

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

    public Task LoggedInUser(Guid userGuid, string name)
    {
        return Task.CompletedTask;
    }

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
}