namespace Linbik.YARP.Configuration;

public sealed class YARPOptions
{
    public string RouteId { get; set; } = string.Empty;
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// Integration service package name for this route
    /// Used to retrieve the correct JWT token from cookies
    /// </summary>
    public string IntegrationPackageName { get; set; } = string.Empty;

    public List<ClusterOptions> Clusters { get; set; } = [];
    public string PrefixPath { get; set; } = string.Empty;

    /// <summary>
    /// Integration services configuration for proxying
    /// Key: PackageName, Value: Service configuration
    /// Proxies requests to Linbik.Server integration endpoints
    /// Example: /api/serverTest/{everything} -> {baseUrl}/api/integration/{everything}
    /// </summary>
    public Dictionary<string, IntegrationServiceOptions> IntegrationServices { get; set; } = new();

    /// <summary>
    /// Cookie name prefix for integration tokens
    /// Default: "integration_"
    /// </summary>
    public string IntegrationTokenCookiePrefix { get; set; } = "integration_";

    /// <summary>
    /// This service's package name (used in Linbik-Source-Package header)
    /// Identifies the calling service in S2S communication
    /// </summary>
    public string? SourcePackageName { get; set; }

    /// <summary>
    /// Default timeout for S2S HTTP requests in seconds
    /// Default: 30
    /// </summary>
    public int S2STimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Relative output directory (from content root) where NSwag-generated Application
    /// clients (see <see cref="IntegrationServiceOptions.DocumentPath"/>) are written.
    /// Default: "Generated"
    /// </summary>
    public string GeneratedClientOutputDirectory { get; set; } = "Generated";

    /// <summary>
    /// Timeout in seconds used when probing an integration service's OpenAPI document
    /// (<see cref="IntegrationServiceOptions.TargetBaseUrl"/> + <see cref="IntegrationServiceOptions.DocumentPath"/>)
    /// before falling back to the previously generated Application client.
    /// Default: 5
    /// </summary>
    public int DocumentCheckTimeoutSeconds { get; set; } = 5;
}

public sealed class ClusterOptions
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for a integration service proxy route
/// Allows proxying requests from one path to another with configurable path rewriting
/// </summary>
public sealed class IntegrationServiceOptions
{

    /// <summary>
    /// Source path pattern (e.g., "api/serverTest")
    /// The {**path} will be automatically appended
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Target base URL (e.g., "https://localhost:5001")
    /// </summary>
    public string TargetBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Target path pattern (e.g., "api/integration")
    /// The captured path will be appended here
    /// </summary>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Timeout in seconds (default: 30)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Path to this integration service's OpenAPI document (e.g., "/openapi/apps.json").
    /// Combined with <see cref="TargetBaseUrl"/> at gateway startup to probe the document and,
    /// if reachable, regenerate a typed Application (S2S) client with NSwag. When left empty,
    /// no client generation is attempted for this service. When the document is unreachable,
    /// the previously generated client (if any) keeps being used as-is.
    /// </summary>
    public string? DocumentPath { get; set; } = "/openapi/apps.json";
}
