using Linbik.Core.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Linbik.Core.Extensions;

/// <summary>Read-only session inspection shared by the JWT and PASETO managers.</summary>
public static class LinbikSessionEndpointExtensions
{
    public static IEndpointConventionBuilder MapLinbikSession(this IEndpointRouteBuilder endpoints, string path)
        => endpoints.MapGet(path, async (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.Vary = "Cookie";
            // Authenticate with the configured validator, never with an unverified token reader.
            var result = await context.AuthenticateAsync(LinbikDefaults.ClientScheme);
            var user = result.Principal;
            var userId = user?.FindFirst("sub")?.Value;
            if (!result.Succeeded || string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var username = user!.FindFirst("preferred_username")?.Value ?? string.Empty;
            return Results.Ok(new LBaseResponse<object>(new
            {
                userId,
                username,
                displayName = user.FindFirst("name")?.Value ?? username,
                // Cookie names are UI hints only, not proof of integration authorization.
                integrations = context.Request.Cookies.Keys
                    .Where(key => key.StartsWith(LinbikDefaults.IntegrationTokenPrefix, StringComparison.Ordinal))
                    .Select(key => key[LinbikDefaults.IntegrationTokenPrefix.Length..]).ToArray()
            }));
        }).AllowAnonymous().WithTags("Linbik");
}
