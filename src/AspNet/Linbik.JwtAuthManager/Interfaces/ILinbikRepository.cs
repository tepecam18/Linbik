using Linbik.Core.Responses;

namespace Linbik.JwtAuthManager.Interfaces;

interface ILinbikRepository
{
    Task CreateRefresToken(Guid userGuid, string name, out string refreshToken);
    Task<TokenValidatorResponse> UseRefresToken(string token);
    Task LoggedInUser(Guid userGuid, string name);
}
