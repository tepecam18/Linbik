using Microsoft.AspNetCore.Authorization;

namespace Linbik.JwtAuthManager.Attributes;

/// <summary>
/// Authorize attribute that uses the Linbik authentication scheme.
/// Validates JWT tokens from the 'authToken' cookie using symmetric key (HS256).
/// 
/// Usage:
/// [LinbikAuthorize]
/// public IActionResult ProtectedEndpoint() { ... }
/// </summary>
public class LinbikAuthorizeAttribute : AuthorizeAttribute
{
    public LinbikAuthorizeAttribute()
    {
        AuthenticationSchemes = "LinbikScheme";
    }
}
