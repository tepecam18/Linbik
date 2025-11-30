namespace Linbik.Core.Models;

/// <summary>
/// Token request body for token exchange and refresh endpoints
/// POST /auth/token: Headers { Code, ApiKey }, Body { ServiceId }
/// POST /auth/refresh: Headers { RefreshToken, ApiKey }, Body { ServiceId }
/// </summary>
public class LinbikTokenRequest
{
    /// <summary>
    /// Service ID (from Linbik service registration)
    /// </summary>
    public Guid ServiceId { get; set; }
}

/// <summary>
/// Token response from Linbik authorization server
/// Returned when exchanging authorization code or refreshing tokens
/// Matches Linbik.App ServiceTokenResponse format
/// </summary>
public class LinbikTokenResponse
{
    /// <summary>
    /// User's profile ID (Guid)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User's profile username (unique identifier)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's display name (nickname)
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// PKCE code challenge (returned for client-side validation)
    /// Client should verify: SHA256(code_verifier) == code_challenge
    /// </summary>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// Original query parameters preserved from authorization request
    /// </summary>
    public string? QueryParameters { get; set; }

    /// <summary>
    /// List of integration service tokens
    /// Each service gets its own JWT token signed with that service's private key
    /// </summary>
    public List<LinbikIntegrationToken>? Integrations { get; set; }

    /// <summary>
    /// Refresh token for obtaining new access tokens
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Refresh token expiration timestamp (Unix epoch seconds)
    /// </summary>
    public long? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Access token expiration timestamp (Unix epoch seconds)
    /// </summary>
    public long? AccessTokenExpiresAt { get; set; }
}

/// <summary>
/// Integration service token data
/// Contains JWT token and metadata for a specific integration service
/// Matches Linbik.App Integration format
/// </summary>
public class LinbikIntegrationToken
{
    /// <summary>
    /// Integration service ID (Guid)
    /// </summary>
    public Guid ServiceId { get; set; }

    /// <summary>
    /// Integration service display name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Integration service package name (URL-safe identifier)
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Integration service URL (e.g., "https://payment-gateway.com/api")
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// JWT access token (signed with integration service's private key)
    /// Use this in Authorization header when calling the integration service
    /// </summary>
    public string Token { get; set; } = string.Empty;

    // Backwards compatibility aliases
    /// <summary>
    /// Alias for ServiceUrl (backwards compatibility)
    /// </summary>
    public string BaseUrl => ServiceUrl;

    /// <summary>
    /// Alias for Token (backwards compatibility)
    /// </summary>
    public string AccessToken => Token;
}

/// <summary>
/// Error response from Linbik authorization server
/// </summary>
public class LinbikErrorResponse
{
    /// <summary>
    /// Error code (e.g., "invalid_grant", "invalid_request")
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error description
    /// </summary>
    public string ErrorDescription { get; set; } = string.Empty;
}
