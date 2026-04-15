namespace Linbik.Core.Models;

/// <summary>
/// Token request body for token exchange and refresh endpoints
/// POST /auth/token: Headers { Code, ApiKey }, Body { ServiceId }
/// POST /auth/refresh: Headers { RefreshToken, ApiKey }, Body { ServiceId }
/// </summary>
public sealed class LinbikTokenRequest
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
public sealed class LinbikTokenResponse
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
    public LinbikTokenExtraData? ExtraData { get; set; }

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

    /// <summary>
    /// OAuth client ID (if applicable)
    /// </summary>
    public Guid? ClientId { get; set; }

    /// <summary>
    /// Whether the provisioned service was successfully claimed by this user.
    /// Only present when a Keyless Mode service is being claimed.
    /// </summary>
    public bool? Claimed { get; set; }

    /// <summary>
    /// New permanent API key issued after claiming a provisioned service.
    /// Only present when Claimed = true. SDK should update local credentials.
    /// </summary>
    public string? NewApiKey { get; set; }
}

public sealed class LinbikTokenExtraData
{
    /// <summary>
    /// Original redirect URL from the authorization request
    /// </summary>
    public string? returnPath { get; set; }
}

/// <summary>
/// Integration service token data
/// Contains JWT token and metadata for a specific integration service
/// Matches Linbik.App Integration format
/// </summary>
public sealed class LinbikIntegrationToken
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
}

/// <summary>
/// Error response from Linbik authorization server
/// </summary>
public sealed class LinbikErrorResponse
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

/// <summary>
/// Request body for the authorization initiate endpoint.
/// POST /api/oauth/initiate
/// Headers: { ApiKey }
/// SDK calls this to start the OAuth flow — saves auth data server-side.
/// </summary>
public sealed class LinbikInitiateRequest
{
    public Guid ClientId { get; set; }
    public string? CodeChallenge { get; set; }
    public Dictionary<string, string>? ExtraData { get; set; }
}

/// <summary>
/// Response from the authorization initiate endpoint.
/// Contains a temporary token and the redirect URL for the browser.
/// </summary>
public sealed class LinbikInitiateResponse
{
    public string Token { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
}

#region S2S (Service-to-Service) Models

/// <summary>
/// Request body for S2S token endpoint
/// POST /auth/s2s-token
/// Headers: { ApiKey }
/// </summary>
public sealed class LinbikS2STokenRequest
{
    /// <summary>
    /// Source service ID (the service requesting tokens)
    /// </summary>
    public Guid SourceServiceId { get; set; }

    /// <summary>
    /// List of target integration service IDs to get tokens for
    /// </summary>
    public List<Guid> TargetServiceIds { get; set; } = [];
}

/// <summary>
/// Response from S2S token endpoint
/// Contains JWT tokens for each requested integration service
/// </summary>
public sealed class LinbikS2STokenResponse
{
    /// <summary>
    /// Source service ID that requested the tokens
    /// </summary>
    public Guid SourceServiceId { get; set; }

    /// <summary>
    /// Source service package name
    /// </summary>
    public string SourcePackageName { get; set; } = string.Empty;

    /// <summary>
    /// List of integration tokens for target services
    /// </summary>
    public List<LinbikS2SIntegration> Integrations { get; set; } = [];

    /// <summary>
    /// Access token expiration timestamp (Unix epoch seconds)
    /// </summary>
    public long AccessTokenExpiresAt { get; set; }
}

/// <summary>
/// S2S integration token data
/// Contains JWT token signed with target service's private key
/// Token contains only service claims (no user information)
/// </summary>
public sealed class LinbikS2SIntegration
{
    /// <summary>
    /// Target integration service ID
    /// </summary>
    public Guid ServiceId { get; set; }

    /// <summary>
    /// Target service display name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Target service package name (URL-safe identifier)
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Target service base URL
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// JWT access token for S2S authentication
    /// Contains: token_type=s2s, source_service_id, source_package_name
    /// Does NOT contain user information
    /// </summary>
    public string Token { get; set; } = string.Empty;
}

#endregion
