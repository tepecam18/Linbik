using Linbik.Core.Responses;
using Linbik.YARP.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AspNet.Controllers;

/// <summary>
/// Demo controller for S2S (Service-to-Service) communication using IS2SServiceClient.
/// This controller demonstrates how to call other Linbik integration services
/// using the S2S client with automatic token injection and LBaseResponse enforcement.
/// 
/// Usage in real services:
/// - Inject IS2SServiceClient in your controllers/services
/// - Use PostAsync/GetAsync with package name for config-based targets
/// - Use PostByIdAsync/GetByIdAsync with service ID for dynamic targets (callbacks/webhooks)
/// </summary>
[ApiController]
[Route("api/s2s-demo")]
public sealed class S2SDemoController(
    IS2SServiceClient s2sClient,
    ILogger<S2SDemoController> logger) : ControllerBase
{
    #region Config-Based S2S Calls (Package Name)

    /// <summary>
    /// Demo: Call another integration service by package name
    /// Uses config-based target service (must be defined in appsettings.json)
    /// 
    /// Example config:
    /// "YARP": {
    ///   "IntegrationServices": {
    ///     "payment-gateway": { "TargetBaseUrl": "https://payment.example.com" }
    ///   }
    /// }
    /// </summary>
    [HttpGet("call-by-package/{packageName}")]
    public async Task<IActionResult> CallByPackageName(string packageName)
    {
        logger.LogInformation("S2S demo: Calling service {PackageName}", packageName);

        // Call the target service's S2S health endpoint
        var result = await s2sClient.GetAsync<S2SHealthResponse>(
            packageName,
            "/api/integration/s2s/health"
        );

        return Ok(new
        {
            demo = "Config-based S2S call by package name",
            targetPackageName = packageName,
            result = new
            {
                result.IsSuccess,
                result.FriendlyMessage,
                result.Data
            },
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Demo: Send data to another service using S2S
    /// Demonstrates POST request with typed request/response
    /// </summary>
    [HttpPost("sync-to/{packageName}")]
    public async Task<IActionResult> SyncToService(string packageName, [FromBody] SyncDataRequest request)
    {
        logger.LogInformation("S2S demo: Syncing data to {PackageName}", packageName);

        // Call the target service's S2S sync endpoint
        var result = await s2sClient.PostAsync<S2SSyncPayload, S2SSyncResponse>(
            packageName,
            "/api/integration/s2s/sync",
            new S2SSyncPayload
            {
                EntityType = request.EntityType ?? "demo",
                EntityId = request.EntityId ?? Guid.NewGuid().ToString(),
                Action = request.Action ?? "sync",
                Data = request.Data
            }
        );

        return Ok(new
        {
            demo = "S2S sync via POST",
            targetPackageName = packageName,
            result = new
            {
                result.IsSuccess,
                result.FriendlyMessage,
                result.Data
            },
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Demo: Send webhook notification to another service
    /// Demonstrates dynamic endpoint path with event type
    /// </summary>
    [HttpPost("webhook-to/{packageName}/{eventType}")]
    public async Task<IActionResult> SendWebhook(string packageName, string eventType, [FromBody] WebhookRequest? request)
    {
        logger.LogInformation("S2S demo: Sending webhook {EventType} to {PackageName}", eventType, packageName);

        var result = await s2sClient.PostAsync<WebhookPayload, WebhookResponse>(
            packageName,
            $"/api/integration/s2s/webhook/{eventType}",
            new WebhookPayload
            {
                EventId = Guid.NewGuid().ToString("N")[..12],
                EntityType = request?.EntityType ?? "order",
                EntityId = request?.EntityId ?? "123",
                Action = eventType,
                Metadata = request?.Metadata
            }
        );

        return Ok(new
        {
            demo = "S2S webhook notification",
            targetPackageName = packageName,
            eventType,
            result = new
            {
                result.IsSuccess,
                result.FriendlyMessage,
                result.Data
            },
            timestamp = DateTime.UtcNow
        });
    }

    #endregion

    #region Dynamic S2S Calls (Service ID) - For Callbacks/Webhooks

    /// <summary>
    /// Demo: Call a service dynamically by its service ID
    /// Does NOT require the target service to be in config
    /// 
    /// Use case: Payment Gateway calling back to merchant service
    /// The merchant service ID is stored in the order/transaction record
    /// </summary>
    [HttpGet("call-by-id/{serviceId:guid}")]
    public async Task<IActionResult> CallByServiceId(Guid serviceId)
    {
        logger.LogInformation("S2S demo: Calling service by ID {ServiceId}", serviceId);

        // Call the target service dynamically by ID
        // Linbik will provide the ServiceUrl in the token response
        var result = await s2sClient.GetByIdAsync<S2SHealthResponse>(
            serviceId,
            "/api/integration/s2s/health"
        );

        return Ok(new
        {
            demo = "Dynamic S2S call by service ID",
            targetServiceId = serviceId,
            result = new
            {
                result.IsSuccess,
                result.FriendlyMessage,
                result.Data
            },
            note = "ServiceUrl fetched from Linbik (not from config)",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Demo: Send callback to a service by its ID
    /// Simulates payment gateway notifying merchant about payment status
    /// </summary>
    [HttpPost("callback-to/{serviceId:guid}")]
    public async Task<IActionResult> SendCallback(Guid serviceId, [FromBody] CallbackRequest request)
    {
        logger.LogInformation("S2S demo: Sending callback to service {ServiceId}", serviceId);

        // This is how Payment Gateway would notify the merchant
        var result = await s2sClient.PostByIdAsync<PaymentCallbackPayload, PaymentCallbackResponse>(
            serviceId,
            "/api/integration/s2s/webhook/payment-completed",
            new PaymentCallbackPayload
            {
                EventId = Guid.NewGuid().ToString("N")[..12],
                EntityType = "payment",
                EntityId = request.TransactionId ?? Guid.NewGuid().ToString(),
                Action = "completed",
                Metadata = new Dictionary<string, object>
                {
                    ["amount"] = request.Amount ?? 0,
                    ["currency"] = request.Currency ?? "TRY",
                    ["status"] = request.Status ?? "completed"
                }
            }
        );

        return Ok(new
        {
            demo = "Dynamic S2S callback (Payment Gateway -> Merchant scenario)",
            targetServiceId = serviceId,
            result = new
            {
                result.IsSuccess,
                result.FriendlyMessage,
                result.Data
            },
            scenario = "Payment Gateway notifying merchant about payment completion",
            timestamp = DateTime.UtcNow
        });
    }

    #endregion

    #region Error Handling Demo

    /// <summary>
    /// Demo: Shows how LBaseResponse handles errors gracefully
    /// When target service is unavailable or returns error
    /// </summary>
    [HttpGet("error-demo/{packageName}")]
    public async Task<IActionResult> ErrorDemo(string packageName)
    {
        logger.LogInformation("S2S demo: Error handling demo for {PackageName}", packageName);

        // Call a non-existent endpoint to trigger error
        var result = await s2sClient.GetAsync<object>(
            packageName,
            "/api/non-existent-endpoint"
        );

        // LBaseResponse provides structured error handling
        return Ok(new
        {
            demo = "S2S error handling",
            targetPackageName = packageName,
            result = new
            {
                result.IsSuccess,
                result.FriendlyMessage,
                hasData = result.Data != null
            },
            note = "LBaseResponse ensures consistent error format across all S2S calls",
            timestamp = DateTime.UtcNow
        });
    }

    #endregion
}

#region DTO Models

// Response Models
public sealed class S2SHealthResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? TargetService { get; set; }
}

public sealed class S2SSyncResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? SyncResult { get; set; }
}

public sealed class WebhookResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? Webhook { get; set; }
}

public sealed class PaymentCallbackResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? Webhook { get; set; }
}

// Request/Payload Models
public sealed class SyncDataRequest
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}

public sealed class S2SSyncPayload
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}

public sealed class WebhookRequest
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public sealed class WebhookPayload
{
    public string? EventId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public sealed class CallbackRequest
{
    public string? TransactionId { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
}

public sealed class PaymentCallbackPayload
{
    public string? EventId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

#endregion
