using System.Text.Json.Serialization;

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
    [JsonPropertyName("event_type")]
    public IntegrationEventType EventType { get; set; }

    /// <summary>
    /// Unique identifier of the integration record in Linbik
    /// </summary>
    [JsonPropertyName("integration_id")]
    public Guid IntegrationId { get; set; }

    /// <summary>
    /// Information about the main service that initiated the integration
    /// Example: "My Blog" wants to use Payment Service
    /// </summary>
    [JsonPropertyName("service")]
    public IntegrationServiceInfo Service { get; set; } = new();

    /// <summary>
    /// Profile designated as admin for this integration on the main service side
    /// The integration service should use this profile for permission management
    /// </summary>
    [JsonPropertyName("admin_profile")]
    public IntegrationAdminProfile AdminProfile { get; set; } = new();

    /// <summary>
    /// Whether the integration is currently enabled for token exchange
    /// </summary>
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; }

    /// <summary>
    /// When this event occurred (UTC)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of integration lifecycle events
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<IntegrationEventType>))]
public enum IntegrationEventType
{
    /// <summary>
    /// A main service registered to use this integration service
    /// </summary>
    [JsonStringEnumMemberName("created")]
    Created,

    /// <summary>
    /// A main service removed its integration with this service
    /// </summary>
    [JsonStringEnumMemberName("removed")]
    Removed,

    /// <summary>
    /// A main service toggled the integration status (enabled/disabled)
    /// </summary>
    [JsonStringEnumMemberName("toggled")]
    Toggled,

    /// <summary>
    /// A main service changed the admin profile for this integration
    /// </summary>
    [JsonStringEnumMemberName("admin_changed")]
    AdminChanged,
    [JsonStringEnumMemberName("service_changed")]
    ServiceChanged
}

/// <summary>
/// Information about the main service that uses the integration
/// </summary>
public sealed class IntegrationServiceInfo
{
    /// <summary>
    /// Main service ID in Linbik platform
    /// </summary>
    [JsonPropertyName("service_id")]
    public Guid ServiceId { get; set; }

    /// <summary>
    /// Display name of the main service
    /// </summary>
    [JsonPropertyName("service_name")]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Unique package name identifier
    /// </summary>
    [JsonPropertyName("package_name")]
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the main service
    /// </summary>
    [JsonPropertyName("base_url")]
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
    [JsonPropertyName("profile_id")]
    public Guid ProfileId { get; set; }

    /// <summary>
    /// Profile username
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Profile display name
    /// </summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}
