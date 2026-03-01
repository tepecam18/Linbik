using Linbik.Server.Models;

namespace Linbik.Server.Interfaces;

/// <summary>
/// Interface for handling integration lifecycle events from Linbik platform.
/// 
/// Implement this interface in your integration service to receive notifications
/// when main services create, remove, toggle, or change admin profiles for integrations.
/// 
/// Usage:
/// <code>
/// public class MyIntegrationHandler : ILinbikIntegrationHandler
/// {
///     public Task&lt;IntegrationEventResult&gt; OnIntegrationCreatedAsync(IntegrationEvent e)
///     {
///         // A new service wants to use our integration
///         // Create records, set up permissions, etc.
///         return Task.FromResult(IntegrationEventResult.Success());
///     }
/// }
/// </code>
/// 
/// Register:
/// <code>
/// builder.Services.AddLinbikIntegrationHandler&lt;MyIntegrationHandler&gt;();
/// </code>
/// </summary>
public interface ILinbikIntegrationHandler
{
    /// <summary>
    /// Called when a main service registers a new integration with this service.
    /// Use this to set up initial permissions, create database records, etc.
    /// </summary>
    /// <param name="integrationEvent">Integration creation event data</param>
    /// <returns>Result indicating success or failure</returns>
    Task<IntegrationEventResult> OnIntegrationCreatedAsync(IntegrationEvent integrationEvent);

    /// <summary>
    /// Called when a main service removes its integration with this service.
    /// Use this to clean up permissions, revoke access, archive records, etc.
    /// </summary>
    /// <param name="integrationEvent">Integration removal event data</param>
    /// <returns>Result indicating success or failure</returns>
    Task<IntegrationEventResult> OnIntegrationRemovedAsync(IntegrationEvent integrationEvent);

    /// <summary>
    /// Called when a main service toggles the integration status (enabled/disabled).
    /// Use this to activate or deactivate service features accordingly.
    /// </summary>
    /// <param name="integrationEvent">Integration toggle event data</param>
    /// <returns>Result indicating success or failure</returns>
    Task<IntegrationEventResult> OnIntegrationToggledAsync(IntegrationEvent integrationEvent);

    /// <summary>
    /// Called when a main service changes the admin profile for this integration.
    /// Use this to update permission assignments, contact info, etc.
    /// </summary>
    /// <param name="integrationEvent">Admin profile change event data</param>
    /// <returns>Result indicating success or failure</returns>
    Task<IntegrationEventResult> OnIntegrationAdminChangedAsync(IntegrationEvent integrationEvent);
}

/// <summary>
/// Result of handling an integration event
/// </summary>
public sealed class IntegrationEventResult
{
    /// <summary>
    /// Whether the event was handled successfully
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Optional message (error details if failed, info if success)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Optional data to return to the caller
    /// </summary>
    public object? Data { get; set; }

    public static IntegrationEventResult Success(string? message = null, object? data = null)
        => new() { IsSuccess = true, Message = message, Data = data };

    public static IntegrationEventResult Failure(string message)
        => new() { IsSuccess = false, Message = message };
}
