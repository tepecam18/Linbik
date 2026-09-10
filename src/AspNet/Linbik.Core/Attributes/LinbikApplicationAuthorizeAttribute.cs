using Microsoft.AspNetCore.Authorization;

namespace Linbik.Core.Attributes;

/// <summary>
/// Authorize attribute for Service-to-Service (apps) requests.
/// Validates JWT tokens that contain only service claims (no user information).
/// Use this when services communicate directly without a user context.
///
/// Token contains: SourceServiceId, SourcePackageName, TargetPackageName, Role
/// Token does NOT contain: UserId, Username, DisplayName
///
/// Usage in integration services:
/// [LinbikApplicationAuthorize]                    // Any apps token accepted
/// public IActionResult SyncInventory() { ... }
///
/// [LinbikApplicationAuthorize("Linbik")]          // Only Linbik platform tokens accepted
/// public IActionResult OnKeyRotation() { ... }
///
/// [LinbikApplicationAuthorize("Service")]         // Only regular service tokens accepted
/// public IActionResult ProcessWebhook() { ... }
///
/// Example scenarios:
/// - Background job synchronization
/// - Service health checks
/// - Batch data processing
/// - Platform-level operations (key rotation, integration lifecycle)
/// </summary>
public sealed class LinbikApplicationAuthorizeAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Accepts any valid apps token regardless of role
    /// </summary>
    public LinbikApplicationAuthorizeAttribute()
    {
        AuthenticationSchemes = LinbikDefaults.ApplicationScheme;
    }

    /// <summary>
    /// Accepts only application tokens with the specified role claim.
    /// Built-in roles: "Linbik" (platform operations), "Service" (regular application)
    /// </summary>
    /// <param name="role">Required role claim value (e.g., "Linbik", "Service")</param>
    public LinbikApplicationAuthorizeAttribute(string role)
    {
        AuthenticationSchemes = LinbikDefaults.ApplicationScheme;
        Roles = role;
    }
}
