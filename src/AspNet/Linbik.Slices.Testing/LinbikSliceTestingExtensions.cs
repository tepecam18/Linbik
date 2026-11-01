using System.Security.Claims;
using System.Text.Encodings.Web;
using Linbik.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Linbik.Slices.Testing;

/// <summary>
/// Test helpers for slices protected by <c>[LFlow]</c> and <c>[LAuthorizeFlow]</c>.
/// Intended for test projects only: the fake handlers trust a request header.
/// </summary>
public static class LinbikSliceTestingExtensions
{
    /// <summary>Header that selects which fake Linbik scheme authenticates the request.</summary>
    public const string TestAuthHeader = "X-Linbik-Test-Auth";

    /// <summary>Header that carries the user id of the fake identity.</summary>
    public const string TestUserHeader = "X-Linbik-Test-User";

    /// <summary>The user id used when none is given to <see cref="AsAuthenticated"/>.</summary>
    public const string DefaultUserId = "test-user";

    /// <summary>
    /// Makes the three Linbik schemes (<c>LinbikScheme</c>, <c>LinbikDelegated</c>, <c>LinbikApplication</c>)
    /// authenticate from the <see cref="TestAuthHeader"/> header instead of real tokens. Schemes the
    /// application already registered are replaced; missing ones are added. Call it from
    /// <c>WebApplicationFactory.ConfigureTestServices</c>.
    /// </summary>
    public static IServiceCollection AddLinbikTestAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthentication();
        services.AddTransient<LinbikTestAuthenticationHandler>();
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            foreach (var scheme in new[] { LinbikDefaults.ClientScheme, LinbikDefaults.DelegatedScheme, LinbikDefaults.ApplicationScheme })
            {
                var existing = options.Schemes.FirstOrDefault(s => s.Name == scheme);
                if (existing is null)
                    options.AddScheme<LinbikTestAuthenticationHandler>(scheme, displayName: null);
                else
                    existing.HandlerType = typeof(LinbikTestAuthenticationHandler);
            }
        });

        return services;
    }

    /// <summary>
    /// Sends the <c>Linbik-Flow</c> header the Gateway would add. Use for <c>[LFlow]</c> slices.
    /// Replaces any previously set value.
    /// </summary>
    public static HttpClient AsFlow(this HttpClient client, LinbikFlow flow)
    {
        ArgumentNullException.ThrowIfNull(client);

        client.DefaultRequestHeaders.Remove(LinbikDefaults.HeaderFlow);
        client.DefaultRequestHeaders.Add(LinbikDefaults.HeaderFlow, flow.ToString());
        return client;
    }

    /// <summary>
    /// Authenticates the client as <paramref name="userId"/> on the scheme of <paramref name="flow"/>.
    /// Use for <c>[LAuthorizeFlow]</c> slices; requires <see cref="AddLinbikTestAuthentication"/>.
    /// </summary>
    public static HttpClient AsAuthenticated(this HttpClient client, LinbikFlow flow, string userId = DefaultUserId)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var scheme = LinbikDefaults.SchemeForFlow(flow.ToString())
            ?? throw new ArgumentOutOfRangeException(nameof(flow), flow, "Unknown Linbik flow.");

        client.DefaultRequestHeaders.Remove(TestAuthHeader);
        client.DefaultRequestHeaders.Remove(TestUserHeader);
        client.DefaultRequestHeaders.Add(TestAuthHeader, scheme);
        client.DefaultRequestHeaders.Add(TestUserHeader, userId);
        return client;
    }

    /// <summary>Removes the headers set by <see cref="AsFlow"/> and <see cref="AsAuthenticated"/>.</summary>
    public static HttpClient AsAnonymous(this HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        client.DefaultRequestHeaders.Remove(LinbikDefaults.HeaderFlow);
        client.DefaultRequestHeaders.Remove(TestAuthHeader);
        client.DefaultRequestHeaders.Remove(TestUserHeader);
        return client;
    }
}

internal sealed class LinbikTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers[LinbikSliceTestingExtensions.TestAuthHeader].Contains(Scheme.Name))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = Request.Headers[LinbikSliceTestingExtensions.TestUserHeader].FirstOrDefault()
            ?? LinbikSliceTestingExtensions.DefaultUserId;

        var identity = new ClaimsIdentity(
            [new Claim("sub", userId), new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, userId)],
            Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
