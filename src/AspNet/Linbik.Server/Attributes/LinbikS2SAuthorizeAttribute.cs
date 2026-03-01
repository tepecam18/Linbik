using Linbik.Core;
using Microsoft.AspNetCore.Authorization;

namespace Linbik.Server.Attributes;

/// <summary>
/// Authorize attribute for Service-to-Service (S2S) requests.
/// Validates JWT tokens that contain only service claims (no user information).
/// Use this when services communicate directly without a user context.
/// 
/// Token contains: SourceServiceId, SourcePackageName, TargetPackageName, Role
/// Token does NOT contain: UserId, Username, DisplayName
/// 
/// Usage in integration services:
/// [LinbikS2SAuthorize]                    // Any S2S token accepted
/// public IActionResult SyncInventory() { ... }
/// 
/// [LinbikS2SAuthorize("Linbik")]          // Only Linbik platform tokens accepted
/// public IActionResult OnKeyRotation() { ... }
/// 
/// [LinbikS2SAuthorize("Service")]         // Only regular service tokens accepted
/// public IActionResult ProcessWebhook() { ... }
/// 
/// Example scenarios:
/// - Background job synchronization
/// - Service health checks
/// - Batch data processing
/// - Platform-level operations (key rotation, integration lifecycle)
/// </summary>
public sealed class LinbikS2SAuthorizeAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Accepts any valid S2S token regardless of role
    /// </summary>
    public LinbikS2SAuthorizeAttribute()
    {
        AuthenticationSchemes = LinbikDefaults.S2SScheme;
    }

    /// <summary>
    /// Accepts only S2S tokens with the specified role claim.
    /// Built-in roles: "Linbik" (platform operations), "Service" (regular S2S)
    /// </summary>
    /// <param name="role">Required role claim value (e.g., "Linbik", "Service")</param>
    public LinbikS2SAuthorizeAttribute(string role)
    {
        AuthenticationSchemes = LinbikDefaults.S2SScheme;
        Roles = role;
    }
}
