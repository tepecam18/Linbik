using Linbik.Core.Models;
using Linbik.YARP.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AspNet.Controllers;

/// <summary>
/// OAuth 2.0 callback and testing controller
/// </summary>
[ApiController]
[Route("oauth")]
public class OAuthCallbackController : Controller
{
    private readonly ITokenProvider _tokenProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OAuthCallbackController> _logger;

    // In-memory storage for demo purposes (use persistent storage in production)
    private static MultiServiceTokenResponse? _cachedTokenResponse;

    public OAuthCallbackController(
        ITokenProvider tokenProvider, 
        IConfiguration configuration,
        ILogger<OAuthCallbackController> logger)
    {
        _tokenProvider = tokenProvider;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// OAuth callback endpoint - receives authorization code from Linbik
    /// GET /oauth/callback?code={auth_code}&state={user_session_code}
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? state = null)
    {
        if (string.IsNullOrEmpty(code))
        {
            _logger.LogError("Authorization code is missing");
            return BadRequest(new { error = "invalid_request", message = "Authorization code is required" });
        }

        _logger.LogInformation("Received authorization code: {Code}, state: {State}", code, state);

        try
        {
            // Get Linbik configuration
            var linbikBaseUrl = _configuration["OAuth:LinbikBaseUrl"] ?? "https://localhost:5001";
            var apiKey = _configuration["OAuth:ApiKey"] ?? throw new InvalidOperationException("API Key not configured");

            _logger.LogInformation("Exchanging authorization code for tokens...");

            // Exchange authorization code for tokens
            var tokenResponse = await _tokenProvider.GetMultiServiceTokenAsync(linbikBaseUrl, code, apiKey);

            if (tokenResponse == null)
            {
                _logger.LogError("Token exchange failed - no response");
                return BadRequest(new { error = "token_exchange_failed", message = "Failed to exchange authorization code" });
            }

            _logger.LogInformation("Token exchange successful. User: {UserName}, Integrations: {Count}", 
                tokenResponse.UserName, tokenResponse.Integrations.Count);

            // Cache the token response
            _tokenProvider.CacheTokenResponse(tokenResponse);
            _cachedTokenResponse = tokenResponse;

            // Return success page with token information
            return Ok(new
            {
                success = true,
                message = "Authentication successful!",
                user = new
                {
                    id = tokenResponse.UserId,
                    username = tokenResponse.UserName,
                    nickname = tokenResponse.NickName
                },
                integrations = tokenResponse.Integrations.Select(i => new
                {
                    serviceId = i.ServiceId,
                    serviceName = i.ServiceName,
                    servicePackage = i.ServicePackage,
                    baseUrl = i.BaseUrl,
                    expiresAt = i.ExpiresAt,
                    hasToken = !string.IsNullOrEmpty(i.Token)
                }).ToList(),
                hasRefreshToken = !string.IsNullOrEmpty(tokenResponse.RefreshToken),
                state = state,
                codeChallenge = tokenResponse.CodeChallenge
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token exchange");
            return StatusCode(500, new { error = "server_error", message = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint to view current token information
    /// GET /oauth/token-info
    /// </summary>
    [HttpGet("token-info")]
    public IActionResult GetTokenInfo()
    {
        if (_cachedTokenResponse == null)
        {
            return NotFound(new { error = "no_tokens", message = "No tokens available. Please authenticate first." });
        }

        return Ok(new
        {
            user = new
            {
                id = _cachedTokenResponse.UserId,
                username = _cachedTokenResponse.UserName,
                nickname = _cachedTokenResponse.NickName
            },
            integrations = _cachedTokenResponse.Integrations.Select(i => new
            {
                serviceId = i.ServiceId,
                serviceName = i.ServiceName,
                servicePackage = i.ServicePackage,
                baseUrl = i.BaseUrl,
                expiresAt = i.ExpiresAt,
                expiresIn = (i.ExpiresAt - DateTime.UtcNow).TotalMinutes,
                isExpired = DateTime.UtcNow > i.ExpiresAt
            }).ToList(),
            refreshToken = new
            {
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(_cachedTokenResponse.RefreshTokenExpiresAt).UtcDateTime,
                expiresIn = (DateTimeOffset.FromUnixTimeSeconds(_cachedTokenResponse.RefreshTokenExpiresAt).UtcDateTime - DateTime.UtcNow).TotalDays,
                isExpired = DateTime.UtcNow > DateTimeOffset.FromUnixTimeSeconds(_cachedTokenResponse.RefreshTokenExpiresAt).UtcDateTime
            }
        });
    }

    /// <summary>
    /// Test endpoint to refresh tokens
    /// POST /oauth/test-refresh
    /// </summary>
    [HttpPost("test-refresh")]
    public async Task<IActionResult> TestRefresh()
    {
        if (_cachedTokenResponse == null)
        {
            return NotFound(new { error = "no_tokens", message = "No tokens available. Please authenticate first." });
        }

        try
        {
            var linbikBaseUrl = _configuration["OAuth:LinbikBaseUrl"] ?? "https://localhost:5001";
            var apiKey = _configuration["OAuth:ApiKey"] ?? throw new InvalidOperationException("API Key not configured");
            var serviceId = _configuration["OAuth:ServiceId"] ?? throw new InvalidOperationException("Service ID not configured");

            _logger.LogInformation("Refreshing tokens...");

            var newTokenResponse = await _tokenProvider.RefreshTokensAsync(
                linbikBaseUrl, 
                _cachedTokenResponse.RefreshToken, 
                apiKey, 
                serviceId);

            if (newTokenResponse == null)
            {
                _logger.LogError("Token refresh failed");
                return BadRequest(new { error = "refresh_failed", message = "Failed to refresh tokens" });
            }

            _logger.LogInformation("Token refresh successful");

            // Update cache
            _tokenProvider.CacheTokenResponse(newTokenResponse);
            _cachedTokenResponse = newTokenResponse;

            return Ok(new
            {
                success = true,
                message = "Tokens refreshed successfully",
                integrations = newTokenResponse.Integrations.Select(i => new
                {
                    serviceName = i.ServiceName,
                    expiresAt = i.ExpiresAt
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { error = "server_error", message = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint to call an integration service
    /// POST /oauth/test-integration/{servicePackage}
    /// </summary>
    [HttpPost("test-integration/{servicePackage}")]
    public async Task<IActionResult> TestIntegration(string servicePackage, [FromBody] object requestData)
    {
        if (_cachedTokenResponse == null)
        {
            return NotFound(new { error = "no_tokens", message = "No tokens available. Please authenticate first." });
        }

        var integration = _cachedTokenResponse.Integrations
            .FirstOrDefault(i => i.ServicePackage == servicePackage);

        if (integration == null)
        {
            return NotFound(new { error = "integration_not_found", message = $"Integration service '{servicePackage}' not found" });
        }

        if (DateTime.UtcNow > integration.ExpiresAt)
        {
            return BadRequest(new { error = "token_expired", message = "Integration token has expired. Please refresh." });
        }

        try
        {
            // Example: Call integration service API
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {integration.Token}");

            var testEndpoint = $"{integration.BaseUrl}/api/test";
            _logger.LogInformation("Calling integration service: {Endpoint}", testEndpoint);

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(testEndpoint, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            return Ok(new
            {
                success = response.IsSuccessStatusCode,
                statusCode = (int)response.StatusCode,
                integration = new
                {
                    serviceName = integration.ServiceName,
                    baseUrl = integration.BaseUrl
                },
                response = responseBody
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling integration service");
            return StatusCode(500, new { error = "integration_call_failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Clear cached tokens (for testing)
    /// POST /oauth/clear-cache
    /// </summary>
    [HttpPost("clear-cache")]
    public IActionResult ClearCache()
    {
        _tokenProvider.ClearCache();
        _cachedTokenResponse = null;
        return Ok(new { success = true, message = "Cache cleared" });
    }
}
