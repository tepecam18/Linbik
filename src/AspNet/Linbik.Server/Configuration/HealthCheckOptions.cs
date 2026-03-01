namespace Linbik.Server.Configuration;

/// <summary>
/// Configuration options for Linbik health checks
/// </summary>
public sealed class LinbikHealthCheckOptions
{
    /// <summary>
    /// Enable health check endpoints (/health, /ready, /live)
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Path for the general health check endpoint
    /// Default: /health
    /// </summary>
    public string HealthPath { get; set; } = "/health";

    /// <summary>
    /// Path for the readiness probe (Kubernetes ready)
    /// Default: /ready
    /// </summary>
    public string ReadyPath { get; set; } = "/ready";

    /// <summary>
    /// Path for the liveness probe (Kubernetes live)
    /// Default: /live
    /// </summary>
    public string LivePath { get; set; } = "/live";

    /// <summary>
    /// Include detailed status in response (disable in production for security)
    /// Default: false
    /// </summary>
    public bool IncludeDetails { get; set; } = false;

    /// <summary>
    /// Custom tags for filtering health checks
    /// </summary>
    public string[] ReadyTags { get; set; } = ["ready"];

    /// <summary>
    /// Custom tags for liveness checks
    /// </summary>
    public string[] LiveTags { get; set; } = ["live"];
}
