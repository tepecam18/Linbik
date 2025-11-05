using Microsoft.AspNetCore.WebUtilities;

namespace AspNet.Helpers;

/// <summary>
/// Helper class for building Linbik authorization URLs
/// </summary>
public class LinbikAuthorizationUrlBuilder
{
    private readonly string _linbikBaseUrl;
    private string? _serviceId;
    private string? _codeChallenge;
    private string? _state;
    private Guid? _profileId;
    private readonly Dictionary<string, string> _additionalParams = new();

    public LinbikAuthorizationUrlBuilder(string linbikBaseUrl)
    {
        if (string.IsNullOrEmpty(linbikBaseUrl))
            throw new ArgumentException("Linbik base URL cannot be null or empty", nameof(linbikBaseUrl));

        _linbikBaseUrl = linbikBaseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Sets the service ID (required)
    /// </summary>
    public LinbikAuthorizationUrlBuilder WithServiceId(string serviceId)
    {
        _serviceId = serviceId;
        return this;
    }

    /// <summary>
    /// Sets the PKCE code challenge (optional but recommended)
    /// </summary>
    public LinbikAuthorizationUrlBuilder WithCodeChallenge(string codeChallenge)
    {
        _codeChallenge = codeChallenge;
        return this;
    }

    /// <summary>
    /// Sets the state parameter for client-side session tracking (optional)
    /// </summary>
    public LinbikAuthorizationUrlBuilder WithState(string state)
    {
        _state = state;
        return this;
    }

    /// <summary>
    /// Sets the profile ID to pre-select a profile (optional)
    /// </summary>
    public LinbikAuthorizationUrlBuilder WithProfileId(Guid profileId)
    {
        _profileId = profileId;
        return this;
    }

    /// <summary>
    /// Adds an additional query parameter
    /// </summary>
    public LinbikAuthorizationUrlBuilder WithParameter(string key, string value)
    {
        _additionalParams[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the authorization URL
    /// </summary>
    /// <returns>Complete authorization URL</returns>
    public string Build()
    {
        if (string.IsNullOrEmpty(_serviceId))
            throw new InvalidOperationException("Service ID is required. Call WithServiceId() first.");

        // Build base path: /auth/{serviceId}/{codeChallenge?}
        var path = $"/auth/{_serviceId}";
        if (!string.IsNullOrEmpty(_codeChallenge))
            path += $"/{_codeChallenge}";

        var url = $"{_linbikBaseUrl}{path}";

        // Add query parameters
        var queryParams = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(_state))
            queryParams["user_session_code"] = _state;

        if (_profileId.HasValue)
            queryParams["profileId"] = _profileId.Value.ToString();

        // Add additional parameters
        foreach (var param in _additionalParams)
        {
            queryParams[param.Key] = param.Value;
        }

        // Build final URL with query string
        if (queryParams.Any())
        {
            url = QueryHelpers.AddQueryString(url, queryParams!);
        }

        return url;
    }

    /// <summary>
    /// Builds the URL with PKCE flow
    /// </summary>
    /// <param name="codeVerifier">Output parameter for the generated code verifier</param>
    /// <returns>Complete authorization URL with code challenge</returns>
    public string BuildWithPkce(out string codeVerifier)
    {
        codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.GenerateCodeChallenge(codeVerifier);
        
        return WithCodeChallenge(codeChallenge).Build();
    }
}

/// <summary>
/// Extension methods for easier URL building
/// </summary>
public static class LinbikAuthorizationUrlBuilderExtensions
{
    /// <summary>
    /// Creates a new LinbikAuthorizationUrlBuilder from IConfiguration
    /// </summary>
    public static LinbikAuthorizationUrlBuilder CreateAuthorizationUrlBuilder(
        this IConfiguration configuration)
    {
        var linbikBaseUrl = configuration["OAuth:LinbikBaseUrl"] 
            ?? throw new InvalidOperationException("OAuth:LinbikBaseUrl not configured");

        return new LinbikAuthorizationUrlBuilder(linbikBaseUrl);
    }
}
