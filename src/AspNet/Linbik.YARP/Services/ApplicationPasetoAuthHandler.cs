using Linbik.YARP.Interfaces;
using Microsoft.Extensions.Logging;

namespace Linbik.YARP.Services;

/// <summary>
/// Delegating handler that injects the Application PASETO bearer token for a specific
/// integration service package into every outgoing request.
/// Backed by <see cref="IApplicationTokenProvider"/>, which obtains PASETO tokens for the
/// Application flow via Linbik.Core's <c>ILinbikAuthClient</c> (never the JWT-based, user-context
/// Delegated/Self flow token provider). Intended to be attached to the named <see cref="HttpClient"/>
/// used by NSwag-generated Application clients (see <see cref="Extensions.LinbikYarpExtensions"/>).
/// Fails closed: a valid token is mandatory for the Application flow, so when one cannot be
/// obtained the request is never sent — <see cref="ApplicationTokenUnavailableException"/> is
/// thrown instead of silently forwarding an unauthenticated request.
/// </summary>
public sealed class ApplicationPasetoAuthHandler(
    string packageName,
    IApplicationTokenProvider tokenProvider,
    ILogger<ApplicationPasetoAuthHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var integration = await tokenProvider.GetApplicationIntegrationAsync(packageName, cancellationToken);

        if (integration is null || string.IsNullOrEmpty(integration.Token))
        {
            logger.LogError(
                "Refusing to send Application request to {PackageName} ({Method} {Url}) — no valid PASETO token available",
                packageName, request.Method, request.RequestUri);
            throw new ApplicationTokenUnavailableException(packageName);
        }

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", integration.Token);

        return await base.SendAsync(request, cancellationToken);
    }
}
