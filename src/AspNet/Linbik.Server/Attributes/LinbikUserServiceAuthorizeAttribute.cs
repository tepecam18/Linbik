using Linbik.Core;
using Microsoft.AspNetCore.Authorization;

namespace Linbik.Server.Attributes;

/// <summary>
/// Authorize attribute for user-initiated service requests.
/// Validates JWT tokens that contain user claims (sub, name, preferred_username, azp).
/// Use this when a main service is making requests on behalf of a user.
/// 
/// Token contains: UserId, Username, DisplayName, AuthorizedParty (main service)
/// 
/// Usage in integration services (Payment Gateway, Courier, etc.):
/// [LinbikUserServiceAuthorize]
/// public IActionResult ProcessPaymentForUser() { ... }
/// </summary>
public sealed class LinbikUserServiceAuthorizeAttribute : AuthorizeAttribute
{
    public LinbikUserServiceAuthorizeAttribute()
    {
        AuthenticationSchemes = LinbikDefaults.UserServiceScheme;
    }
}
