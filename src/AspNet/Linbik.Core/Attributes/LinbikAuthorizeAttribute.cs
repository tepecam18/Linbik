using Microsoft.AspNetCore.Authorization;

namespace Linbik.Core.Attributes;

/// <summary>
/// Authorize attribute that uses the Linbik authentication scheme.
/// Validates JWT tokens from the 'authToken' cookie using symmetric key (HS256).
///
/// Usage:
/// [LinbikAuthorize]
/// public IActionResult ProtectedEndpoint() { ... }
/// </summary>
public sealed class LinbikAuthorizeAttribute : AuthorizeAttribute
{
    public LinbikAuthorizeAttribute()
    {
        AuthenticationSchemes = LinbikDefaults.ClientScheme;
    }
}
