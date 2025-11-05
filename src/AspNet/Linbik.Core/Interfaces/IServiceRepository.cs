namespace Linbik.Core.Interfaces;

/// <summary>
/// Repository interface for service/application management
/// </summary>
public interface IServiceRepository
{
    /// <summary>
    /// Gets a service by its unique ID
    /// </summary>
    Task<ServiceData?> GetServiceByIdAsync(Guid serviceId);

    /// <summary>
    /// Gets a service by its API key
    /// </summary>
    Task<ServiceData?> GetServiceByApiKeyAsync(string apiKey);

    /// <summary>
    /// Gets a service by its package name
    /// </summary>
    Task<ServiceData?> GetServiceByPackageNameAsync(string packageName);

    /// <summary>
    /// Gets all integration services granted by a user for a specific main service
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="mainServiceId">Main service ID</param>
    /// <returns>List of granted integration services</returns>
    Task<List<ServiceData>> GetGrantedIntegrationServicesAsync(Guid userId, Guid mainServiceId);

    /// <summary>
    /// Gets all integration services that a main service can use
    /// </summary>
    /// <param name="mainServiceId">Main service ID</param>
    /// <returns>List of available integration services</returns>
    Task<List<ServiceData>> GetAvailableIntegrationServicesAsync(Guid mainServiceId);

    /// <summary>
    /// Checks if a service is active and not deleted
    /// </summary>
    Task<bool> IsServiceActiveAsync(Guid serviceId);

    /// <summary>
    /// Validates if an IP address is allowed for a service
    /// </summary>
    /// <param name="serviceId">Service ID</param>
    /// <param name="ipAddress">IP address to validate</param>
    /// <returns>True if allowed or no IP restrictions, false otherwise</returns>
    Task<bool> IsIpAllowedAsync(Guid serviceId, string ipAddress);
}

/// <summary>
/// Service data model
/// </summary>
public class ServiceData
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? AllowedIPs { get; set; }
    public bool IsIntegrationService { get; set; }
    public string? Description { get; set; }
    public string PrivateKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
