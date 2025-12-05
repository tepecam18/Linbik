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
    /// </summary>
    public Dictionary<string, IntegrationServiceOptions> IntegrationServices { get; set; } = new();

    /// <summary>
    /// Cookie name prefix for integration tokens
    /// Default: "integration_"
    /// </summary>
    public string IntegrationTokenCookiePrefix { get; set; } = "integration_";
}

public class ClusterOptions
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for a specific integration service
/// </summary>
public class IntegrationServiceOptions
{
    /// <summary>
    /// Integration service package name (unique identifier)
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Integration service base URL for proxying
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Service ID (GUID) for validation
    /// </summary>
    public Guid ServiceId { get; set; }

    /// <summary>
    /// Optional: Timeout in seconds (default: 30)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Optional: Whether to preserve the original path after package name
    /// Default: true (e.g., /payment/charge -> /charge)
    /// </summary>
    public bool StripPackagePrefix { get; set; } = true;
}
