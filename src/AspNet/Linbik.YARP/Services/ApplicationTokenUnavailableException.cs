namespace Linbik.YARP.Services;

/// <summary>
/// Thrown by <see cref="ApplicationPasetoAuthHandler"/> when a valid Application (apps) PASETO
/// token could not be obtained for an integration service. Application requests must never be
/// sent without a valid bearer token — this exception is thrown before the underlying HTTP
/// request is dispatched, so the call never reaches the target service unauthenticated.
/// </summary>
public sealed class ApplicationTokenUnavailableException(string packageName)
    : Exception(
        $"Unable to obtain an Application (apps) PASETO token for integration service '{packageName}'. " +
        "The request was not sent — verify that the package name is correct and that Linbik is reachable.")
{
    /// <summary>The integration service package name the token could not be obtained for.</summary>
    public string PackageName { get; } = packageName;
}
