using Linbik.YARP.Interfaces;

namespace Linbik.YARP.Services;

/// <summary>
/// Delegating handler that injects the Application (S2S) PASETO bearer token for a specific
/// integration service package into every outgoing request.
/// Backed by <see cref="IApplicationTokenProvider"/>, which obtains PASETO tokens for the
/// Application flow via Linbik.Core's <c>ILinbikAuthClient</c> (never the JWT-based, user-context
/// Delegated/Self flow token provider). Intended to be attached to the named <see cref="HttpClient"/>
/// used by NSwag-generated Application clients (see <see cref="Extensions.LinbikYarpExtensions"/>).
/// </summary>
public sealed class ApplicationPasetoAuthHandler(
    string packageName,
    IApplicationTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var integration = await tokenProvider.GetApplicationIntegrationAsync(packageName, cancellationToken);

        if (integration is not null && !string.IsNullOrEmpty(integration.Token))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", integration.Token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
