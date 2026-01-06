namespace Linbik.YARP.Configuration;

public class YARPOptions
{
    public string RouteId { get; set; } = string.Empty;
    public string ClusterId { get; set; } = string.Empty;

    /// <summary>
    /// Integration service package name for this route
    /// Used to retrieve the correct JWT token from cookies
    /// </summary>
    public string IntegrationPackageName { get; set; } = string.Empty;

    public List<ClusterOptions> Clusters { get; set; } = new();
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
}

/// <summary>

/// </summary>


public class ClusterOptions
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for a integration service proxy route
/// Allows proxying requests from one path to another with configurable path rewriting
/// </summary>
public class IntegrationServiceOptions
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
}
