using Linbik.Core.Interfaces;
using Linbik.JwtAuthManager.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Linbik.JwtAuthManager.Middleware;

/// <summary>
/// Middleware to handle Linbik login redirects and OAuth callback
/// Replaces manual JWT generation with Linbik authorization flow
/// </summary>
public class LinbikAuthMiddleware(RequestDelegate next, IOptions<JwtAuthOptions> options)
{
    private readonly RequestDelegate _next = next;
    private readonly JwtAuthOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

        // Handle login endpoint - redirect to Linbik
        if (path == _options.LoginPath.ToLower())
        {
            var returnUrl = context.Request.Query["returnUrl"].ToString();
            var codeChallenge = context.Request.Query["code_challenge"].ToString();

            await authService.RedirectToLinbikAsync(context, returnUrl, codeChallenge);
            return;
        }

        // Handle OAuth callback from Linbik
        if (path == "/linbik/callback")
        {
            var code = context.Request.Query["code"].ToString();
            
            if (string.IsNullOrEmpty(code))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Missing authorization code");
                return;
            }

            // Exchange code for tokens
            var tokenResponse = await authService.ExchangeCodeForTokensAsync(code);
            
            if (tokenResponse == null)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Failed to exchange authorization code");
                return;
            }

            // Get return URL from session
            var returnUrl = context.Session.GetString("linbik_return_url");
            context.Session.Remove("linbik_return_url");

            // Redirect to return URL or home
            context.Response.Redirect(returnUrl ?? "/");
            return;
        }

        // Handle logout endpoint
        if (path == _options.ExitPath.ToLower())
        {
            await authService.LogoutAsync(context);
            context.Response.Redirect("/");
            return;
        }

        // Continue to next middleware
        await _next(context);
    }
}

/// <summary>
/// Extension methods for LinbikAuthMiddleware
/// </summary>
public static class LinbikAuthMiddlewareExtensions
{
    /// <summary>
    /// Add Linbik authentication middleware to handle login/logout/callback
    /// </summary>
    public static IApplicationBuilder UseLinbikAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<LinbikAuthMiddleware>();
    }
}
