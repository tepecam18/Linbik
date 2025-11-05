using Linbik.YARP.Interfaces;
using Microsoft.AspNetCore.Mvc;
using AspNet.Helpers;

namespace AspNet.Controllers;

/// <summary>
/// PKCE test controller for demonstrating secure authorization flow
/// </summary>
[ApiController]
[Route("pkce")]
public class PkceTestController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<PkceTestController> _logger;

    // In-memory storage for demo (use session/cache in production)
    private static readonly Dictionary<string, string> _codeVerifiers = new();

    public PkceTestController(
        IConfiguration configuration,
        ITokenProvider tokenProvider,
        ILogger<PkceTestController> logger)
    {
        _configuration = configuration;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    /// <summary>
    /// Step 1: Generate PKCE parameters and authorization URL
    /// GET /pkce/start
    /// </summary>
    [HttpGet("start")]
    public IActionResult StartPkceFlow([FromQuery] string? state = null)
    {
        try
        {
            var serviceId = _configuration["OAuth:ServiceId"] 
                ?? throw new InvalidOperationException("Service ID not configured");

            // Build authorization URL with PKCE
            var urlBuilder = _configuration.CreateAuthorizationUrlBuilder()
                .WithServiceId(serviceId);

            if (!string.IsNullOrEmpty(state))
                urlBuilder.WithState(state);

            var authorizationUrl = urlBuilder.BuildWithPkce(out var codeVerifier);

            // Generate a session ID to track this PKCE flow
            var sessionId = Guid.NewGuid().ToString();
            _codeVerifiers[sessionId] = codeVerifier;

            _logger.LogInformation("PKCE flow started. SessionId: {SessionId}", sessionId);

            return Ok(new
            {
                sessionId,
                authorizationUrl,
                codeVerifier, // In production, don't expose this - store securely client-side
                instructions = "1. Visit authorizationUrl in browser. 2. After callback, use sessionId to validate."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting PKCE flow");
            return StatusCode(500, new { error = "pkce_start_failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Step 2: Validate PKCE challenge after receiving callback
    /// POST /pkce/validate
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidatePkceFlow([FromBody] PkceValidationRequest request)
    {
        if (string.IsNullOrEmpty(request.SessionId) || string.IsNullOrEmpty(request.AuthorizationCode))
        {
            return BadRequest(new { error = "invalid_request", message = "SessionId and AuthorizationCode are required" });
        }

        try
        {
            // Retrieve stored code verifier
            if (!_codeVerifiers.TryGetValue(request.SessionId, out var codeVerifier))
            {
                return BadRequest(new { error = "invalid_session", message = "Session not found or expired" });
            }

            _logger.LogInformation("Validating PKCE for session: {SessionId}", request.SessionId);

            // Exchange authorization code for tokens
            var linbikBaseUrl = _configuration["OAuth:LinbikBaseUrl"] ?? "https://localhost:5001";
            var apiKey = _configuration["OAuth:ApiKey"] 
                ?? throw new InvalidOperationException("API Key not configured");

            var tokenResponse = await _tokenProvider.GetMultiServiceTokenAsync(
                linbikBaseUrl,
                request.AuthorizationCode,
                apiKey);

            if (tokenResponse == null)
            {
                return BadRequest(new { error = "token_exchange_failed", message = "Failed to exchange authorization code" });
            }

            // PKCE Validation: Verify code challenge
            var isValid = PkceHelper.ValidateCodeChallenge(codeVerifier, tokenResponse.CodeChallenge ?? "");

            if (!isValid)
            {
                _logger.LogWarning("PKCE validation failed for session: {SessionId}", request.SessionId);
                return BadRequest(new
                {
                    error = "pkce_validation_failed",
                    message = "Code challenge validation failed - possible security attack!",
                    details = new
                    {
                        expectedChallenge = PkceHelper.GenerateCodeChallenge(codeVerifier),
                        receivedChallenge = tokenResponse.CodeChallenge
                    }
                });
            }

            // Clean up
            _codeVerifiers.Remove(request.SessionId);

            _logger.LogInformation("PKCE validation successful for session: {SessionId}", request.SessionId);

            // Cache tokens
            _tokenProvider.CacheTokenResponse(tokenResponse);

            return Ok(new
            {
                success = true,
                message = "PKCE validation successful! ✅",
                pkceValidation = new
                {
                    verified = true,
                    codeVerifier,
                    codeChallenge = tokenResponse.CodeChallenge
                },
                user = new
                {
                    id = tokenResponse.UserId,
                    username = tokenResponse.UserName,
                    nickname = tokenResponse.NickName
                },
                integrations = tokenResponse.Integrations.Select(i => new
                {
                    serviceName = i.ServiceName,
                    servicePackage = i.ServicePackage,
                    expiresAt = i.ExpiresAt
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating PKCE flow");
            return StatusCode(500, new { error = "pkce_validation_error", message = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint: Generate PKCE parameters without starting a flow
    /// GET /pkce/generate
    /// </summary>
    [HttpGet("generate")]
    public IActionResult GeneratePkceParameters()
    {
        var codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.GenerateCodeChallenge(codeVerifier);

        return Ok(new
        {
            codeVerifier,
            codeChallenge,
            method = "S256",
            instructions = new[]
            {
                "1. Store codeVerifier securely client-side (sessionStorage/localStorage)",
                "2. Send codeChallenge to Linbik authorization endpoint",
                "3. After callback, validate: SHA256(codeVerifier) === returned codeChallenge"
            }
        });
    }

    /// <summary>
    /// Cleanup endpoint: Clear stored code verifiers
    /// POST /pkce/clear
    /// </summary>
    [HttpPost("clear")]
    public IActionResult ClearPkceStorage()
    {
        var count = _codeVerifiers.Count;
        _codeVerifiers.Clear();
        return Ok(new { success = true, message = $"Cleared {count} stored code verifiers" });
    }
}

public class PkceValidationRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string AuthorizationCode { get; set; } = string.Empty;
}
