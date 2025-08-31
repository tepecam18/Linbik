using Linbik.Core.Responses;

namespace Linbik.JwtAuthManager.Interfaces;

public interface ILinbikRepository
{
    /// <summary>
    /// Creates a refresh token for a user and saves it to the database
    /// </summary>
    /// <param name="userGuid">The user's unique identifier</param>
    /// <param name="name">The user's name</param>
    /// <returns>Tuple containing the refresh token and success status</returns>
    Task<(string refreshToken, bool success)> CreateRefresToken(Guid userGuid, string name);

    //TODO: sadece name yok
    /// <summary>
    /// Logs a user login and manages user data in the database
    /// </summary>
    /// <param name="userGuid">The user's unique identifier</param>
    /// <param name="name">The user's name</param>
    Task<TokenValidatorResponse> UseRefresToken(string token);

    /// <summary>
    /// Validates a refresh token against the database
    /// </summary>
    /// <param name="token">The refresh token to validate</param>
    /// <returns>Token validation response</returns>
    Task LoggedInUser(Guid userGuid, string name);
}
