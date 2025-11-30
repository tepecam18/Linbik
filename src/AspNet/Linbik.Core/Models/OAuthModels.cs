namespace Linbik.Core.Models;

/// <summary>
/// Multi-service token response (YARP compatibility)
/// Maps to LinbikTokenResponse with different property names for YARP proxy
/// </summary>
public class MultiServiceTokenResponse
{
    /// <summary>
    /// User's unique identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User's profile username (maps to LinbikTokenResponse.Username)
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// User's display name (maps to LinbikTokenResponse.DisplayName)
    /// </summary>
    public string NickName { get; set; } = string.Empty;

    /// <summary>
    /// List of integration service tokens
    /// </summary>
    public List<IntegrationToken> Integrations { get; set; } = new();

    /// <summary>
    /// Refresh token for obtaining new access tokens
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token expiration timestamp (Unix epoch seconds)
    /// </summary>
    public long RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// PKCE code challenge (returned for client-side validation)
    /// </summary>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// Original query parameters preserved from authorization request
    /// </summary>
    public string? QueryParameters { get; set; }

    /// <summary>
    /// Convert from LinbikTokenResponse
    /// </summary>
    public static MultiServiceTokenResponse FromLinbikResponse(LinbikTokenResponse response)
    {
        return new MultiServiceTokenResponse
        {
            UserId = response.UserId,
            UserName = response.Username,
            NickName = response.DisplayName,
            RefreshToken = response.RefreshToken ?? string.Empty,
            RefreshTokenExpiresAt = response.RefreshTokenExpiresAt ?? 0,
            CodeChallenge = response.CodeChallenge,
            QueryParameters = response.QueryParameters,
            Integrations = response.Integrations?.Select(i => new IntegrationToken
            {
                ServiceId = i.ServiceId,
                ServiceName = i.ServiceName,
                ServicePackage = i.PackageName,
                BaseUrl = i.ServiceUrl,
                Token = i.Token
            }).ToList() ?? new()
        };
    }
}

/// <summary>
/// Integration service token data (YARP compatibility)
/// Maps to LinbikIntegrationToken with different property names
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
    /// Integration service package name (maps to PackageName)
    /// </summary>
    public string ServicePackage { get; set; } = string.Empty;

    /// <summary>
    /// Integration service base URL (maps to ServiceUrl)
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// JWT access token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration timestamp
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Request model for refresh token endpoint (body format for some clients)
/// Note: Linbik.App uses headers for RefreshToken and ApiKey
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
    /// Service ID
    /// </summary>
    public string CommunityId { get; set; } = string.Empty;
}
