using Linbik.Core.Responses;
using Linbik.JwtAuthManager.Interfaces;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Linbik.JwtAuthManager;

class InMemoryLinbikRepository(IOptions<JwtAuthOptions> jwtOptions) : ILinbikRepository
{
    public List<TokenModel> tokens { get; set; } = new List<TokenModel>();


    public Task CreateRefresToken(Guid userGuid, string name, out string refreshToken)
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        refreshToken = Convert.ToBase64String(randomNumber);

        var token = new TokenModel
        {
            refreshToken = refreshToken,
            expiration = DateTime.UtcNow.AddDays(jwtOptions.Value.refreshTokenExpiration),
            userGuid = userGuid,
            name = name
        };

        tokens.Add(token);

        return Task.CompletedTask;
    }

    public Task LoggedInUser(Guid userGuid, string name)
    {
        return Task.CompletedTask;
    }

    public async Task<TokenValidatorResponse> UseRefresToken(string token)
    {
        var tokenData = tokens.FirstOrDefault(t => t.refreshToken == token);
        if (tokenData == null || tokenData.expiration < DateTime.UtcNow)
        {
            return new TokenValidatorResponse
            {
                Success = false,
                Message = "Invalid or expired refresh token."
            };
        }

        return new TokenValidatorResponse
        {
            Success = true,
            UserGuid = tokenData.userGuid,
            Name = tokenData.name,
        };

    }
}


public class TokenModel
    {
    public string refreshToken { get; set; }
    public DateTime expiration { get; set; }
    public Guid userGuid { get; set; }
    public string name { get; set; }
}
