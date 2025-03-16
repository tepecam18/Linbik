using Linbik.JwtAuthManager.Interfaces;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Linbik.JwtAuthManager;

class InMemoryLinbikRepository : ILinbikRepository
{
    public Task CreateRefresToken(Guid userGuid, string name, out string refreshToken, out List<Claim> claims)
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        refreshToken = Convert.ToBase64String(randomNumber);

        claims = new List<Claim>();

        return Task.CompletedTask;
    }

    public Task LoggedInUser(Guid userGuid, string name)
    {
        return Task.CompletedTask;
    }

    public async Task<TokenValidatorResponse> UseRefresToken(string token)
    {
        return new()
        {

        };
    }
}
