using Linbik.Server.Attributes;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AspNet.Controllers;

/// <summary>
/// Demo Integration Controller for Linbik.Server library.
/// This controller demonstrates how integration services (Payment Gateway, Survey, etc.)
/// can implement protected and public endpoints using LinbikIntegrationAuthorize attribute.
/// 
/// Usage in real integration services:
/// - Copy this pattern to your own integration service
/// - Use [LinbikIntegrationAuthorize] for endpoints that require user authentication
/// - Public endpoints can be accessed without authentication
/// </summary>
[ApiController]
[Route("api/integration")]
public class IntegrationController : ControllerBase
{
    #region Public Endpoints (No Authentication Required)

    /// <summary>
    /// Health check endpoint - no authentication required
    /// Use this to verify the integration service is running
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            success = true,
            status = "healthy",
            service = "Linbik.Server Integration",
            version = "1.0.0",
            timestamp = DateTime.UtcNow,
            message = "✅ Public endpoint - no authentication required"
        });
    }

    /// <summary>
    /// Service info endpoint - no authentication required
    /// Returns public information about this integration service
    /// </summary>
    [HttpGet("info")]
    public IActionResult Info()
    {
        return Ok(new
        {
            success = true,
            service = new
            {
                name = "Linbik Integration Service Demo",
                description = "Demonstrates LinbikIntegrationAuthorize attribute for protected endpoints",
                version = "1.0.0",
                endpoints = new
                {
                    @public = new[]
                    {
                        "GET /api/integration/health - Health check",
                        "GET /api/integration/info - Service information",
                        "GET /api/integration/public-data - Public sample data"
                    },
                    @protected = new[]
                    {
                        "GET /api/integration/protected - Requires valid Linbik JWT",
                        "GET /api/integration/user-profile - Returns authenticated user profile",
                        "POST /api/integration/process - Process data for authenticated user"
                    }
                }
            },
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Public data endpoint - no authentication required
    /// Returns sample public data that anyone can access
    /// </summary>
    [HttpGet("public-data")]
    public IActionResult PublicData()
    {
        return Ok(new
        {
            success = true,
            message = "✅ Public data accessible without authentication",
            data = new
            {
                sampleItems = new[]
                {
                    new { id = 1, name = "Public Item 1", category = "demo" },
                    new { id = 2, name = "Public Item 2", category = "demo" },
                    new { id = 3, name = "Public Item 3", category = "demo" }
                },
                generatedAt = DateTime.UtcNow
            }
        });
    }

    /// <summary>
    /// Echo endpoint - no authentication required
    /// Returns the request headers and query parameters for debugging
    /// </summary>
    [HttpGet("echo")]
    public IActionResult Echo()
    {
        var headers = Request.Headers
            .Where(h => !h.Key.StartsWith("Cookie", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        var queryParams = Request.Query
            .ToDictionary(q => q.Key, q => q.Value.ToString());

        return Ok(new
        {
            success = true,
            message = "✅ Echo endpoint - shows request info",
            request = new
            {
                method = Request.Method,
                path = Request.Path.Value,
                headers,
                queryParams,
                hasAuthorizationHeader = Request.Headers.ContainsKey("Authorization"),
                contentType = Request.ContentType,
                contentLength = Request.ContentLength
            },
            timestamp = DateTime.UtcNow
        });
    }

    #endregion

    #region Protected Endpoints (Requires LinbikIntegrationAuthorize)

    /// <summary>
    /// Protected endpoint - requires valid Linbik JWT token
    /// Uses [LinbikIntegrationAuthorize] attribute which validates RS256 signed JWT
    /// </summary>
    [LinbikIntegrationAuthorize]
    [HttpGet("protected")]
    public IActionResult Protected()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = User.FindFirst(ClaimTypes.Name)?.Value;
        var displayName = User.FindFirst("display_name")?.Value;

        return Ok(new
        {
            success = true,
            message = "✅ Protected endpoint accessed with valid Linbik JWT!",
            authScheme = "LinbikIntegration (RS256)",
            user = new
            {
                userId,
                userName,
                displayName
            },
            claimCount = User.Claims.Count(),
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// User profile endpoint - requires authentication
    /// Returns full user profile from JWT claims
    /// </summary>
    [LinbikIntegrationAuthorize]
    [HttpGet("user-profile")]
    public IActionResult UserProfile()
    {
        var claims = User.Claims.Select(c => new
        {
            type = c.Type,
            value = c.Value
        }).ToList();

        return Ok(new
        {
            success = true,
            message = "✅ User profile retrieved from JWT claims",
            profile = new
            {
                userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                userName = User.FindFirst(ClaimTypes.Name)?.Value,
                displayName = User.FindFirst("display_name")?.Value,
                email = User.FindFirst(ClaimTypes.Email)?.Value,
                isAuthenticated = User.Identity?.IsAuthenticated ?? false,
                authenticationType = User.Identity?.AuthenticationType
            },
            allClaims = claims,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Process data endpoint - requires authentication
    /// Demonstrates a POST endpoint that processes user data
    /// </summary>
    [LinbikIntegrationAuthorize]
    [HttpPost("process")]
    public IActionResult Process([FromBody] ProcessRequest? request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = User.FindFirst(ClaimTypes.Name)?.Value;

        return Ok(new
        {
            success = true,
            message = "✅ Data processed successfully for authenticated user",
            result = new
            {
                processedBy = "Linbik.Server Integration",
                forUser = new { userId, userName },
                inputData = request,
                outputData = new
                {
                    status = "completed",
                    transactionId = Guid.NewGuid().ToString("N")[..8],
                    processedAt = DateTime.UtcNow
                }
            },
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// User data endpoint - requires authentication
    /// Returns personalized data for the authenticated user
    /// </summary>
    [LinbikIntegrationAuthorize]
    [HttpGet("user-data")]
    public IActionResult UserData()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Simulate fetching user-specific data
        var userData = new
        {
            userId,
            preferences = new
            {
                theme = "dark",
                language = "tr",
                notifications = true
            },
            recentActivity = new[]
            {
                new { action = "login", timestamp = DateTime.UtcNow.AddHours(-1) },
                new { action = "profile_view", timestamp = DateTime.UtcNow.AddHours(-2) },
                new { action = "data_export", timestamp = DateTime.UtcNow.AddDays(-1) }
            },
            quotaUsage = new
            {
                used = 150,
                limit = 1000,
                unit = "MB"
            }
        };

        return Ok(new
        {
            success = true,
            message = "✅ User-specific data retrieved",
            data = userData,
            timestamp = DateTime.UtcNow
        });
    }

    #endregion
}

/// <summary>
/// Request model for the Process endpoint
/// </summary>
public class ProcessRequest
{
    public string? Action { get; set; }
    public string? Data { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
