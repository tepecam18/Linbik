using Linbik.Core.Responses;

namespace Linbik.YARP.Interfaces;

/// <summary>
/// HTTP client interface for S2S (Service-to-Service) communication
/// Automatically injects S2S tokens and enforces LBaseResponse format
/// Supports both config-based (package name) and dynamic (service ID) targets
/// </summary>
public interface IS2SServiceClient
{
    #region Package Name Based (Config-based targets)

    /// <summary>
    /// Send GET request to integration service by package name
    /// </summary>
    /// <typeparam name="TResponse">Expected response data type</typeparam>
    /// <param name="packageName">Target integration service package name</param>
    /// <param name="endpoint">API endpoint path (e.g., "/api/users/123")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse with typed data</returns>
    Task<LBaseResponse<TResponse>> GetAsync<TResponse>(
        string packageName,
        string endpoint,
        CancellationToken cancellationToken = default) where TResponse : class;

    /// <summary>
    /// Send POST request to integration service by package name
    /// </summary>
    /// <typeparam name="TRequest">Request body type</typeparam>
    /// <typeparam name="TResponse">Expected response data type</typeparam>
    /// <param name="packageName">Target integration service package name</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="request">Request body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse with typed data</returns>
    Task<LBaseResponse<TResponse>> PostAsync<TRequest, TResponse>(
        string packageName,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// Send POST request without response data by package name
    /// </summary>
    /// <typeparam name="TRequest">Request body type</typeparam>
    /// <param name="packageName">Target integration service package name</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="request">Request body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse without data</returns>
    Task<LBaseResponse<object>> PostAsync<TRequest>(
        string packageName,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default) where TRequest : class;

    /// <summary>
    /// Send PUT request to integration service by package name
    /// </summary>
    /// <typeparam name="TRequest">Request body type</typeparam>
    /// <typeparam name="TResponse">Expected response data type</typeparam>
    /// <param name="packageName">Target integration service package name</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="request">Request body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse with typed data</returns>
    Task<LBaseResponse<TResponse>> PutAsync<TRequest, TResponse>(
        string packageName,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// Send DELETE request to integration service by package name
    /// </summary>
    /// <typeparam name="TResponse">Expected response data type</typeparam>
    /// <param name="packageName">Target integration service package name</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse with typed data</returns>
    Task<LBaseResponse<TResponse>> DeleteAsync<TResponse>(
        string packageName,
        string endpoint,
        CancellationToken cancellationToken = default) where TResponse : class;

    /// <summary>
    /// Send DELETE request without response data by package name
    /// </summary>
    /// <param name="packageName">Target integration service package name</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse without data</returns>
    Task<LBaseResponse<object>> DeleteAsync(
        string packageName,
        string endpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send PATCH request to integration service by package name
    /// </summary>
    /// <typeparam name="TRequest">Request body type</typeparam>
    /// <typeparam name="TResponse">Expected response data type</typeparam>
    /// <param name="packageName">Target integration service package name</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="request">Request body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse with typed data</returns>
    Task<LBaseResponse<TResponse>> PatchAsync<TRequest, TResponse>(
        string packageName,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;

    #endregion

    #region Service ID Based (Dynamic targets - for callbacks/webhooks)

    /// <summary>
    /// Send GET request to a dynamically specified service by ID
    /// Does NOT require service to be in config - fetches token and URL from Linbik
    /// Use this for callbacks/webhooks where target service is not pre-configured
    /// </summary>
    /// <typeparam name="TResponse">Expected response data type</typeparam>
    /// <param name="targetServiceId">Target service ID (GUID)</param>
    /// <param name="endpoint">API endpoint path (e.g., "/api/webhook/payment")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse with typed data</returns>
    Task<LBaseResponse<TResponse>> GetByIdAsync<TResponse>(
        Guid targetServiceId,
        string endpoint,
        CancellationToken cancellationToken = default) where TResponse : class;

    /// <summary>
    /// Send POST request to a dynamically specified service by ID
    /// Does NOT require service to be in config
    /// </summary>
    /// <typeparam name="TRequest">Request body type</typeparam>
    /// <typeparam name="TResponse">Expected response data type</typeparam>
    /// <param name="targetServiceId">Target service ID (GUID)</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="request">Request body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse with typed data</returns>
    Task<LBaseResponse<TResponse>> PostByIdAsync<TRequest, TResponse>(
        Guid targetServiceId,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// Send POST request without response data to a dynamically specified service by ID
    /// </summary>
    /// <typeparam name="TRequest">Request body type</typeparam>
    /// <param name="targetServiceId">Target service ID (GUID)</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="request">Request body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse without data</returns>
    Task<LBaseResponse<object>> PostByIdAsync<TRequest>(
        Guid targetServiceId,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default) where TRequest : class;

    /// <summary>
    /// Send PUT request to a dynamically specified service by ID
    /// </summary>
    /// <typeparam name="TRequest">Request body type</typeparam>
    /// <typeparam name="TResponse">Expected response data type</typeparam>
    /// <param name="targetServiceId">Target service ID (GUID)</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="request">Request body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse with typed data</returns>
    Task<LBaseResponse<TResponse>> PutByIdAsync<TRequest, TResponse>(
        Guid targetServiceId,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// Send DELETE request to a dynamically specified service by ID
    /// </summary>
    /// <typeparam name="TResponse">Expected response data type</typeparam>
    /// <param name="targetServiceId">Target service ID (GUID)</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse with typed data</returns>
    Task<LBaseResponse<TResponse>> DeleteByIdAsync<TResponse>(
        Guid targetServiceId,
        string endpoint,
        CancellationToken cancellationToken = default) where TResponse : class;

    /// <summary>
    /// Send DELETE request without response data to a dynamically specified service by ID
    /// </summary>
    /// <param name="targetServiceId">Target service ID (GUID)</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse without data</returns>
    Task<LBaseResponse<object>> DeleteByIdAsync(
        Guid targetServiceId,
        string endpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send PATCH request to a dynamically specified service by ID
    /// </summary>
    /// <typeparam name="TRequest">Request body type</typeparam>
    /// <typeparam name="TResponse">Expected response data type</typeparam>
    /// <param name="targetServiceId">Target service ID (GUID)</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="request">Request body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LBaseResponse with typed data</returns>
    Task<LBaseResponse<TResponse>> PatchByIdAsync<TRequest, TResponse>(
        Guid targetServiceId,
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;

    #endregion
}
