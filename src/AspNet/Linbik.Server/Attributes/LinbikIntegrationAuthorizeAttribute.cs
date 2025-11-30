using Microsoft.AspNetCore.Authorization;

namespace Linbik.Server.Extensions;

/// <summary>
/// Authorize attribute for Linbik integration service endpoints.
/// Validates JWT tokens using RSA public key (RS256).
/// 
/// Usage in integration services (Payment Gateway, Courier, etc.):
/// [LinbikIntegrationAuthorize]
/// public IActionResult ProcessPayment() { ... }
/// </summary>
public class LinbikIntegrationAuthorizeAttribute : AuthorizeAttribute
{
    public LinbikIntegrationAuthorizeAttribute()
    {
        AuthenticationSchemes = "LinbikIntegration";
    }
}
