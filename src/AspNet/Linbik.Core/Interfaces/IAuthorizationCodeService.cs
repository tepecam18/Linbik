namespace Linbik.Core.Interfaces;

/// <summary>
/// Service for managing authorization codes
/// </summary>
public interface IAuthorizationCodeService
{
    /// <summary>
    /// Generates a secure authorization code
    /// </summary>
    /// <param name="serviceId">Service requesting authorization</param>
    /// <param name="userId">Authenticated user ID</param>
    /// <param name="profileId">User's profile ID</param>
    /// <param name="grantedIntegrationServiceIds">List of granted integration service IDs</param>
    /// <param name="codeChallenge">PKCE code challenge (optional)</param>
    /// <param name="userSessionCode">User's custom session code (optional)</param>
    /// <param name="queryParameters">Additional query parameters to preserve</param>
    /// <param name="clientIp">Client IP address for validation</param>
    /// <param name="expirationMinutes">Code expiration time in minutes (default: 10)</param>
    /// <returns>Generated authorization code</returns>
    Task<string> GenerateCodeAsync(
        Guid serviceId,
        Guid userId,
        Guid profileId,
        List<Guid> grantedIntegrationServiceIds,
        string? codeChallenge = null,
        string? userSessionCode = null,
        string? queryParameters = null,
        string? clientIp = null,
        int expirationMinutes = 10);

    /// <summary>
    /// Validates an authorization code and marks it as used
    /// </summary>
    /// <param name="code">Authorization code to validate</param>
    /// <param name="serviceId">Service attempting to use the code</param>
    /// <returns>Tuple containing validation result and code data (if valid)</returns>
    Task<(bool isValid, AuthorizationCodeData? data)> ValidateAndUseCodeAsync(string code, Guid serviceId);

    /// <summary>
    /// Checks if a code is still valid (not used and not expired)
    /// </summary>
    /// <param name="code">Authorization code</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> IsCodeValidAsync(string code);
}

/// <summary>
/// Data returned when validating an authorization code
/// </summary>
public class AuthorizationCodeData
{
    public Guid UserId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid ServiceId { get; set; }
    public List<Guid> GrantedIntegrationServiceIds { get; set; } = new();
    public string? CodeChallenge { get; set; }
    public string? UserSessionCode { get; set; }
    public string? QueryParameters { get; set; }
    public string? ClientIp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
