using Linbik.Core.Interfaces;
using Linbik.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Linbik.JwtAuthManager.Services;

/// <summary>
/// Legacy JWT auth service (deprecated - use LinbikAuthService from Linbik.Core instead)
/// </summary>
[Obsolete("Use LinbikAuthService from Linbik.Core for authentication")]
class JwtAuthService : IAuthService
{
    #region Not Implemented - Use LinbikAuthService

    public Task RedirectToLinbikAsync(HttpContext context, string? returnUrl = null, string? codeChallenge = null)
    {
        throw new NotImplementedException("Use LinbikAuthService from Linbik.Core instead");
    }

    public Task<LinbikTokenResponse?> ExchangeCodeForTokensAsync(string code)
    {
        throw new NotImplementedException("Use LinbikAuthService from Linbik.Core instead");
    }

    public Task<UserProfile?> GetUserProfileAsync(HttpContext context)
    {
        throw new NotImplementedException("Use LinbikAuthService from Linbik.Core instead");
    }

    public Task<List<LinbikIntegrationToken>> GetIntegrationTokensAsync(HttpContext context)
    {
        throw new NotImplementedException("Use LinbikAuthService from Linbik.Core instead");
    }

    public Task<bool> RefreshTokensAsync(HttpContext context)
    {
        throw new NotImplementedException("Use LinbikAuthService from Linbik.Core instead");
    }

    public Task LogoutAsync(HttpContext context)
    {
        throw new NotImplementedException("Use LinbikAuthService from Linbik.Core instead");
    }

    #endregion
}
