using Linbik.Server.Interfaces;
using Linbik.Server.Models;
using Microsoft.Extensions.Logging;

namespace Linbik.Server.Services;

/// <summary>
/// Default implementation of integration event handler.
/// Override virtual methods in your own class to customize behavior.
/// 
/// Usage:
/// <code>
/// public class PaymentIntegrationHandler(
///     PaymentDbContext db,
///     ILogger&lt;PaymentIntegrationHandler&gt; logger) : LinbikIntegrationHandler(logger)
/// {
///     public override async Task&lt;IntegrationEventResult&gt; OnIntegrationCreatedAsync(IntegrationEvent e)
///     {
///         // Create merchant record for the new service
///         var merchant = new Merchant
///         {
///             ExternalServiceId = e.Service.ServiceId,
///             ServiceName = e.Service.ServiceName,
///             AdminUsername = e.AdminProfile.Username,
///             AdminProfileId = e.AdminProfile.ProfileId,
///             IsActive = true
///         };
///         db.Merchants.Add(merchant);
///         await db.SaveChangesAsync();
///         
///         return IntegrationEventResult.Success("Merchant created");
///     }
/// }
/// </code>
/// </summary>
public class LinbikIntegrationHandler(ILogger<LinbikIntegrationHandler>? logger = null) : ILinbikIntegrationHandler
{
    protected readonly ILogger<LinbikIntegrationHandler>? Logger = logger;

    /// <inheritdoc />
    public virtual Task<IntegrationEventResult> OnIntegrationCreatedAsync(IntegrationEvent integrationEvent)
    {
        Logger?.LogInformation(
            "Integration created: Service '{ServiceName}' ({ServiceId}) registered with admin profile '{AdminUsername}'",
            integrationEvent.Service.ServiceName,
            integrationEvent.Service.ServiceId,
            integrationEvent.AdminProfile.Username);

        return Task.FromResult(IntegrationEventResult.Success());
    }

    /// <inheritdoc />
    public virtual Task<IntegrationEventResult> OnIntegrationRemovedAsync(IntegrationEvent integrationEvent)
    {
        Logger?.LogInformation(
            "Integration removed: Service '{ServiceName}' ({ServiceId}) disconnected",
            integrationEvent.Service.ServiceName,
            integrationEvent.Service.ServiceId);

        return Task.FromResult(IntegrationEventResult.Success());
    }

    /// <inheritdoc />
    public virtual Task<IntegrationEventResult> OnIntegrationToggledAsync(IntegrationEvent integrationEvent)
    {
        Logger?.LogInformation(
            "Integration toggled: Service '{ServiceName}' ({ServiceId}) is now {Status}",
            integrationEvent.Service.ServiceName,
            integrationEvent.Service.ServiceId,
            integrationEvent.IsEnabled ? "enabled" : "disabled");

        return Task.FromResult(IntegrationEventResult.Success());
    }

    /// <inheritdoc />
    public virtual Task<IntegrationEventResult> OnIntegrationAdminChangedAsync(IntegrationEvent integrationEvent)
    {
        Logger?.LogInformation(
            "Integration admin changed: Service '{ServiceName}' ({ServiceId}) new admin '{AdminUsername}'",
            integrationEvent.Service.ServiceName,
            integrationEvent.Service.ServiceId,
            integrationEvent.AdminProfile.Username);

        return Task.FromResult(IntegrationEventResult.Success());
    }
}
