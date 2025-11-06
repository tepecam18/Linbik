using Linbik.Server.Interfaces;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Linbik.Server.Middleware;

/// <summary>
/// Middleware to validate JWT tokens for integration services
/// Extracts token from Authorization header and validates with RSA public key
/// Sets User context if validation succeeds
/// </summary>
public class IntegrationAuthMiddleware
{
    private readonly RequestDelegate _next;

    public IntegrationAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IntegrationTokenValidator validator,
        ILinbikServerRepository repository)
    {
        // Skip validation for anonymous endpoints
        var endpoint = context.GetEndpoint();
        var allowAnonymous = endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null;
        
        if (allowAnonymous)
        {
            await _next(context);
            return;
        }

        // Get public key from repository (should be cached)
        // Integration services store their own public key
        var serviceId = context.Request.Headers["X-Service-Id"].ToString();
        if (string.IsNullOrEmpty(serviceId) || !Guid.TryParse(serviceId, out var serviceGuid))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Missing or invalid X-Service-Id header");
            return;
        }

        var service = await repository.GetServiceByIdAsync(serviceGuid);
        if (service == null || string.IsNullOrEmpty(service.PublicKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Service not found or missing public key");
            return;
        }

        // Validate token
        var principal = await validator.ValidateTokenAsync(context, service.PublicKey);
        if (principal == null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid or expired token");
            return;
        }

        // Set user context
        context.User = principal;

        // Continue to next middleware
        await _next(context);
    }
}

/// <summary>
/// Extension methods for IntegrationAuthMiddleware
/// </summary>
public static class IntegrationAuthMiddlewareExtensions
{
    /// <summary>
    /// Use integration authentication middleware for JWT validation
    /// </summary>
    public static IApplicationBuilder UseIntegrationAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<IntegrationAuthMiddleware>();
    }
}
