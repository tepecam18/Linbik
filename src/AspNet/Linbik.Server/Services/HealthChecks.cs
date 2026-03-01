using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Linbik.Server.Services;

/// <summary>
/// Health check for Linbik integration service authentication
/// Verifies that the RSA public key is loaded and JWT validation is ready
/// </summary>
public sealed class LinbikAuthHealthCheck(IntegrationTokenValidator tokenValidator) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if token validator is properly configured
            var isHealthy = tokenValidator.IsConfigured();

            if (isHealthy)
            {
                return Task.FromResult(HealthCheckResult.Healthy(
                    "Linbik authentication is configured and ready.",
                    new Dictionary<string, object>
                    {
                        ["validator_status"] = "ready",
                        ["timestamp"] = DateTime.UtcNow
                    }));
            }

            return Task.FromResult(HealthCheckResult.Degraded(
                "Linbik authentication is not fully configured.",
                data: new Dictionary<string, object>
                {
                    ["validator_status"] = "not_configured",
                    ["timestamp"] = DateTime.UtcNow
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Linbik authentication check failed.",
                ex,
                new Dictionary<string, object>
                {
                    ["validator_status"] = "error",
                    ["error"] = ex.Message,
                    ["timestamp"] = DateTime.UtcNow
                }));
        }
    }
}

/// <summary>
/// Simple liveness check - always returns healthy if the app is running
/// </summary>
public sealed class LinbikLivenessHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy(
            "Application is alive.",
            new Dictionary<string, object>
            {
                ["status"] = "alive",
                ["timestamp"] = DateTime.UtcNow
            }));
    }
}

/// <summary>
/// Readiness check - verifies all dependencies are ready
/// </summary>
public sealed class LinbikReadinessHealthCheck(IntegrationTokenValidator tokenValidator) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var checks = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow
        };

        // Check 1: Token validator configured
        var validatorReady = tokenValidator.IsConfigured();
        checks["auth_validator"] = validatorReady ? "ready" : "not_configured";

        // All checks passed?
        if (validatorReady)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Service is ready to accept traffic.",
                checks));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy(
            "Service is not ready. Some dependencies are not configured.",
            data: checks));
    }
}
