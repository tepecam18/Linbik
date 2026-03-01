using Microsoft.AspNetCore.Http;

namespace Linbik.Core.Extensions;

/// <summary>
/// Extension methods for HttpContext operations shared across Linbik packages
/// </summary>
public static class LinbikHttpContextExtensions
{
    /// <summary>
    /// Get the client IP address from the HTTP context.
    /// Checks X-Forwarded-For and X-Real-IP headers first (reverse proxy scenarios),
    /// then falls back to RemoteIpAddress.
    /// </summary>
    public static string? GetClientIpAddress(this HttpContext context)
    {
        // Check for forwarded headers first (for reverse proxy scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain (original client)
            return forwardedFor.Split(',').First().Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
