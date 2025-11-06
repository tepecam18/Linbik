using Linbik.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Linbik.JwtAuthManager.Services;

/// <summary>
/// Legacy JWT auth service (deprecated - use LinbikClient from Linbik.Core instead)
/// </summary>
[Obsolete("Use LinbikClient from Linbik.Core for OAuth 2.0 authentication")]
class JwtAuthService : IAuthService
{
    private const string IdClaimType = "userId";

    [Obsolete("Use GetUserProfileAsync instead")]
    public async Task<string> GetUserIdAsync(HttpContext context)
    {
        // Kullanıcı doğrulanmış mı kontrol ediyoruz
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            // En yaygın kullanılan claim türü: ClaimTypes.NameIdentifier
            var userId = context.User.FindFirst(IdClaimType)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                return userId;
            }
        }

        // Eğer kullanıcı doğrulanmamışsa veya id bulunamazsa null döner.
        return string.Empty;
    }

    #region New OAuth 2.0 Methods (Not Implemented - Use LinbikClient)

    public Task RedirectToLinbikAsync(HttpContext context, string? returnUrl = null, string? codeChallenge = null)
    {
        throw new NotImplementedException("Use LinbikClient from Linbik.Core instead");
    }

    public Task<TokenResponse?> ExchangeCodeForTokensAsync(string code)
    {
        throw new NotImplementedException("Use LinbikClient from Linbik.Core instead");
    }

    public Task<UserProfile?> GetUserProfileAsync(HttpContext context)
    {
        throw new NotImplementedException("Use LinbikClient from Linbik.Core instead");
    }

    public Task<List<IntegrationToken>> GetIntegrationTokensAsync(HttpContext context)
    {
        throw new NotImplementedException("Use LinbikClient from Linbik.Core instead");
    }

    public Task<bool> RefreshTokensAsync(HttpContext context)
    {
        throw new NotImplementedException("Use LinbikClient from Linbik.Core instead");
    }

    public Task LogoutAsync(HttpContext context)
    {
        throw new NotImplementedException("Use LinbikClient from Linbik.Core instead");
    }

    #endregion
}
