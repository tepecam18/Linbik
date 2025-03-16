using System.Security.Claims;

namespace Linbik.JwtAuthManager.Interfaces;

interface ILinbikRepository
{
    Task CreateRefresToken(Guid userGuid, string name, out string refreshToken, out List<Claim> claims);
    Task<TokenValidatorResponse> UseRefresToken(string token);
    Task LoggedInUser(Guid userGuid, string name);
}
