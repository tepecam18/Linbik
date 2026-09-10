using Linbik.Server.Interfaces;
using Linbik.Server.Models;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Linbik.Server.Extensions;

/// <summary>Handles integration events independently of routing and authorization registration.</summary>
internal static class IntegrationEventEndpoints
{
    internal static async Task<IResult> CreateAsync(IntegrationEvent integrationEvent, ILinbikIntegrationHandler handler, ILogger<LinbikIntegrationHandler> logger)
    {
        try
        {
            integrationEvent.EventType = IntegrationEventType.Created;
            var result = await handler.OnIntegrationCreatedAsync(integrationEvent);

            return ToHttpResult(result, "Integration registered");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling integration created event for {IntegrationId}", integrationEvent.IntegrationId);
            return Results.StatusCode(500);
        }
    }

    internal static async Task<IResult> RemoveAsync(Guid integrationId, HttpRequest request, ILinbikIntegrationHandler handler, ILogger<LinbikIntegrationHandler> logger)
    {
        try
        {
            // Read body for full event data (sent by Linbik.App notification service)
            IntegrationEvent? integrationEvent = null;
            try
            {
                integrationEvent = await request.ReadFromJsonAsync<IntegrationEvent>();
            }
            catch
            {
                // Body may be empty — construct minimal event
            }

            integrationEvent ??= new IntegrationEvent();
            integrationEvent.IntegrationId = integrationId;
            integrationEvent.EventType = IntegrationEventType.Removed;
            integrationEvent.Timestamp = DateTime.UtcNow;

            var result = await handler.OnIntegrationRemovedAsync(integrationEvent);

            return ToHttpResult(result, "Integration removed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling integration removed event for {IntegrationId}", integrationId);
            return Results.StatusCode(500);
        }
    }

    internal static async Task<IResult> ToggleStatusAsync(Guid integrationId, IntegrationEvent integrationEvent, ILinbikIntegrationHandler handler, ILogger<LinbikIntegrationHandler> logger)
    {
        try
        {
            integrationEvent.IntegrationId = integrationId;
            integrationEvent.EventType = IntegrationEventType.Toggled;

            var result = await handler.OnIntegrationToggledAsync(integrationEvent);

            return ToHttpResult(result, "Integration status updated");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling integration toggled event for {IntegrationId}", integrationId);
            return Results.StatusCode(500);
        }
    }

    internal static async Task<IResult> ChangeAdminAsync(Guid integrationId, IntegrationEvent integrationEvent, ILinbikIntegrationHandler handler, ILogger<LinbikIntegrationHandler> logger)
    {
        try
        {
            integrationEvent.IntegrationId = integrationId;
            integrationEvent.EventType = IntegrationEventType.AdminChanged;

            var result = await handler.OnIntegrationAdminChangedAsync(integrationEvent);

            return ToHttpResult(result, "Integration admin updated");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling integration admin changed event for {IntegrationId}", integrationId);
            return Results.StatusCode(500);
        }
    }
    internal static async Task<IResult> ChangeServiceAsync(Guid integrationId, IntegrationEvent integrationEvent, ILinbikIntegrationHandler handler, ILogger<LinbikIntegrationHandler> logger)
    {
        try
        {
            integrationEvent.IntegrationId = integrationId;
            integrationEvent.EventType = IntegrationEventType.ServiceChanged;
            return ToHttpResult(await handler.OnIntegrationServiceChangedAsync(integrationEvent), "Integration service updated");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling service change for {IntegrationId}", integrationId);
            return Results.StatusCode(500);
        }
    }

    private static IResult ToHttpResult(IntegrationEventResult result, string successMessage) =>
        result.IsSuccess
            ? Results.Ok(new { success = true, message = result.Message ?? successMessage })
            : Results.BadRequest(new { success = false, message = result.Message });
}
