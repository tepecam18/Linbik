namespace Linbik.Server.Models;

/// <summary>
/// Represents an integration lifecycle event sent from Linbik platform to integration services.
/// When a main service adds/removes/toggles an integration, Linbik notifies the integration service
/// so it can manage permissions and settings on its own platform.
/// </summary>
public sealed class IntegrationEvent
{
    /// <summary>
    /// Type of the integration event
    /// </summary>
    public IntegrationEventType EventType { get; set; }

    /// <summary>
    /// Unique identifier of the integration record in Linbik
    /// </summary>
    public Guid IntegrationId { get; set; }

    /// <summary>
    /// Information about the main service that initiated the integration
    /// Example: "My Blog" wants to use Payment Service
    /// </summary>
    public IntegrationServiceInfo Service { get; set; } = new();

    /// <summary>
    /// Profile designated as admin for this integration on the main service side
    /// The integration service should use this profile for permission management
    /// </summary>
    public IntegrationAdminProfile AdminProfile { get; set; } = new();

    /// <summary>
    /// Whether the integration is currently enabled for token exchange
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// When this event occurred (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of integration lifecycle events
/// </summary>
public enum IntegrationEventType
{
    /// <summary>
    /// A main service registered to use this integration service
    /// </summary>
    Created,

    /// <summary>
    /// A main service removed its integration with this service
    /// </summary>
    Removed,

    /// <summary>
    /// A main service toggled the integration status (enabled/disabled)
    /// </summary>
    Toggled,

    /// <summary>
    /// A main service changed the admin profile for this integration
    /// </summary>
    AdminChanged
}

/// <summary>
/// Information about the main service that uses the integration
/// </summary>
public sealed class IntegrationServiceInfo
{
    /// <summary>
    /// Main service ID in Linbik platform
    /// </summary>
    public Guid ServiceId { get; set; }

    /// <summary>
    /// Display name of the main service
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Unique package name identifier
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the main service
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// Admin profile information for the integration
/// The integration service should use this profile as the administrative contact
/// </summary>
public sealed class IntegrationAdminProfile
{
    /// <summary>
    /// Profile ID in Linbik platform
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>
    /// Profile username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Profile display name (nickname)
    /// </summary>
    public string? DisplayName { get; set; }
}
