using Linbik.Core.Responses;

namespace Linbik.JwtAuthManager.Interfaces;

public interface ILinbikRepository
{
    Task<(string refreshToken, bool success)> CreateRefresToken(Guid userGuid, string name);
    Task<TokenValidatorResponse> UseRefresToken(string token);
    Task LoggedInUser(Guid userGuid, string name);
}
