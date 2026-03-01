using Linbik.Server.Attributes;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AspNet.Controllers;

/// <summary>
/// Demo Integration Controller for Linbik.Server library.
/// This controller demonstrates how integration services (Payment Gateway, Survey, etc.)
/// can implement protected endpoints using Linbik authorization attributes.
/// 
/// Usage in real integration services:
/// - Copy this pattern to your own integration service
/// - Use [LinbikUserServiceAuthorize] for endpoints that require user context (user-initiated requests)
/// - Use [LinbikS2SAuthorize] for service-to-service endpoints (no user context)
/// - Public endpoints can be accessed without authentication
/// </summary>
[ApiController]
[Route("api/integration")]
public sealed class IntegrationController : ControllerBase
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
                description = "Demonstrates LinbikUserServiceAuthorize and LinbikS2SAuthorize attributes for protected endpoints",
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

    #region Protected Endpoints (Requires LinbikUserServiceAuthorize)

    /// <summary>
    /// Protected endpoint - requires valid Linbik JWT token with user context
    /// Uses [LinbikUserServiceAuthorize] attribute which validates RS256 signed JWT
    /// </summary>
    [LinbikUserServiceAuthorize]
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
    [LinbikUserServiceAuthorize]
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
    [LinbikUserServiceAuthorize]
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
    [LinbikUserServiceAuthorize]
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

    #region S2S Protected Endpoints (Requires LinbikS2SAuthorize)

    /// <summary>
    /// S2S sync endpoint - requires valid S2S JWT token (no user context)
    /// Uses [LinbikS2SAuthorize] attribute which validates RS256 signed JWT
    /// 
    /// Scenario: Another service calls this endpoint to sync data
    /// Example: Payment Gateway syncing transaction status with this service
    /// </summary>
    [LinbikS2SAuthorize]
    [HttpPost("s2s/sync")]
    public IActionResult S2SSync([FromBody] S2SSyncRequest? request)
    {
        // Extract S2S claims (no user information!)
        var sourceServiceId = User.FindFirst("source_service_id")?.Value;
        var sourcePackageName = User.FindFirst("source_package_name")?.Value;
        var tokenType = User.FindFirst("token_type")?.Value;

        return Ok(new
        {
            success = true,
            message = "✅ S2S sync endpoint accessed with valid S2S JWT!",
            authScheme = "LinbikS2S (RS256)",
            sourceService = new
            {
                serviceId = sourceServiceId,
                packageName = sourcePackageName,
                tokenType
            },
            syncResult = new
            {
                entityType = request?.EntityType ?? "unknown",
                entityId = request?.EntityId ?? "unknown",
                action = request?.Action ?? "sync",
                status = "synced",
                syncId = Guid.NewGuid().ToString("N")[..8],
                syncedAt = DateTime.UtcNow
            },
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// S2S health check endpoint - requires valid S2S JWT token
    /// Used by other services to verify this service is accessible
    /// 
    /// Scenario: Service discovery or health monitoring between services
    /// </summary>
    [LinbikS2SAuthorize]
    [HttpGet("s2s/health")]
    public IActionResult S2SHealth()
    {
        var sourceServiceId = User.FindFirst("source_service_id")?.Value;
        var sourcePackageName = User.FindFirst("source_package_name")?.Value;

        return Ok(new
        {
            success = true,
            message = "✅ S2S health check - service is accessible",
            authScheme = "LinbikS2S (RS256)",
            sourceService = new
            {
                serviceId = sourceServiceId,
                packageName = sourcePackageName
            },
            targetService = new
            {
                name = "Linbik Integration Service Demo",
                status = "healthy",
                uptime = TimeSpan.FromHours(24).ToString()
            },
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// S2S webhook endpoint - receives callbacks from other services
    /// Uses [LinbikS2SAuthorize] to ensure only authenticated services can call
    /// 
    /// Scenario: Payment Gateway notifying about payment completion
    /// Example: POST /api/integration/s2s/webhook/payment-completed
    /// </summary>
    [LinbikS2SAuthorize("Service")]
    [HttpPost("s2s/webhook/{eventType}")]
    public IActionResult S2SWebhook(string eventType, [FromBody] S2SWebhookPayload? payload)
    {
        var sourceServiceId = User.FindFirst("source_service_id")?.Value;
        var sourcePackageName = User.FindFirst("source_package_name")?.Value;

        // Log webhook received
        var webhookId = Guid.NewGuid().ToString("N")[..12];

        return Ok(new
        {
            success = true,
            message = $"✅ S2S webhook received: {eventType}",
            webhook = new
            {
                id = webhookId,
                eventType,
                sourceService = new
                {
                    serviceId = sourceServiceId,
                    packageName = sourcePackageName
                },
                payload = payload != null ? new
                {
                    payload.EventId,
                    payload.EntityType,
                    payload.EntityId,
                    payload.Action,
                    metadataKeys = payload.Metadata?.Keys.ToArray()
                } : null,
                status = "processed",
                processedAt = DateTime.UtcNow
            },
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// S2S batch operation endpoint - processes batch data from other services
    /// 
    /// Scenario: Bulk data synchronization between services
    /// Example: Inventory service sending batch stock updates
    /// </summary>
    [LinbikS2SAuthorize]
    [HttpPost("s2s/batch")]
    public IActionResult S2SBatch([FromBody] S2SBatchRequest? request)
    {
        var sourceServiceId = User.FindFirst("source_service_id")?.Value;
        var sourcePackageName = User.FindFirst("source_package_name")?.Value;

        var itemCount = request?.Items?.Count ?? 0;
        var batchId = Guid.NewGuid().ToString("N")[..8];

        return Ok(new
        {
            success = true,
            message = $"✅ S2S batch processed: {itemCount} items",
            batch = new
            {
                batchId,
                sourceService = new
                {
                    serviceId = sourceServiceId,
                    packageName = sourcePackageName
                },
                operation = request?.Operation ?? "unknown",
                itemsReceived = itemCount,
                itemsProcessed = itemCount,
                itemsFailed = 0,
                status = "completed",
                processedAt = DateTime.UtcNow
            },
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Platform-only endpoint - receives Linbik platform lifecycle events
    /// Uses [LinbikS2SAuthorize("Linbik")] to ONLY accept tokens from the Linbik platform
    /// Regular service-to-service tokens will be rejected (403 Forbidden)
    /// 
    /// Scenario: Linbik platform notifying about key rotation, integration toggle, etc.
    /// Example: POST /api/integration/s2s/platform-event
    /// </summary>
    [LinbikS2SAuthorize("Linbik")]
    [HttpPost("s2s/platform-event")]
    public IActionResult S2SPlatformEvent([FromBody] S2SWebhookPayload? payload)
    {
        var sourcePackageName = User.FindFirst("source_package_name")?.Value;

        return Ok(new
        {
            success = true,
            message = "✅ Platform event received (Linbik role verified)",
            source = sourcePackageName,
            eventType = payload?.Action ?? "unknown",
            processedAt = DateTime.UtcNow
        });
    }

    #endregion
}

#region Request Models

/// <summary>
/// Request model for the Process endpoint
/// </summary>
public sealed class ProcessRequest
{
    public string? Action { get; set; }
    public string? Data { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Request model for S2S sync endpoint
/// </summary>
public sealed class S2SSyncRequest
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Webhook payload model for S2S webhook endpoint
/// </summary>
public sealed class S2SWebhookPayload
{
    public string? EventId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Batch request model for S2S batch endpoint
/// </summary>
public sealed class S2SBatchRequest
{
    public string? Operation { get; set; }
    public List<Dictionary<string, object>>? Items { get; set; }
}

#endregion
