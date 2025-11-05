namespace Linbik.Core.Models;

/// <summary>
/// OAuth 2.0 multi-service token response
/// Returned when exchanging authorization code for tokens
/// </summary>
public class MultiServiceTokenResponse
{
    /// <summary>
    /// User's unique identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User's profile username
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// User's display name (nickname)
    /// </summary>
    public string NickName { get; set; } = string.Empty;

    /// <summary>
    /// List of integration service tokens
    /// Each service gets its own JWT token signed with that service's private key
    /// </summary>
    public List<IntegrationToken> Integrations { get; set; } = new();

    /// <summary>
    /// Refresh token for obtaining new access tokens (30 days validity)
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token expiration timestamp (Unix epoch seconds)
    /// </summary>
    public long RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// PKCE code challenge (returned for client-side validation)
    /// Client should verify this matches their SHA-256 hash of code_verifier
    /// </summary>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// Original query parameters preserved from authorization request
    /// </summary>
    public string? QueryParameters { get; set; }
}

/// <summary>
/// Integration service token data
/// Contains JWT token and metadata for a specific integration service
/// </summary>
public class IntegrationToken
{
    /// <summary>
    /// Integration service ID
    /// </summary>
    public Guid ServiceId { get; set; }

    /// <summary>
    /// Integration service display name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Integration service package name (URL-safe identifier)
    /// </summary>
    public string ServicePackage { get; set; } = string.Empty;

    /// <summary>
    /// Integration service base URL
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// JWT access token (signed with integration service's private key)
    /// Use this in Authorization header when calling the integration service
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token lifetime in seconds (typically 3600 = 1 hour)
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Token expiration timestamp
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Request model for refresh token endpoint
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Refresh token obtained from initial token exchange
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Service API key for authentication
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Service ID (community ID)
    /// </summary>
    public string CommunityId { get; set; } = string.Empty;
}
