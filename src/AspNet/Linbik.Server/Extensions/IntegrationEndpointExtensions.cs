using Linbik.Server.Interfaces;
using Linbik.Server.Models;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Linbik.Server.Extensions;

/// <summary>
/// Well-known endpoint sub-paths for Linbik integration lifecycle events.
/// These must match the paths used by Linbik.App's IntegrationNotificationService.
/// 
/// Full endpoint = {BaseUrl}{IntegrationPath}{SubPath}
/// Default IntegrationPath = "/api/external"
/// </summary>
public static class LinbikIntegrationEndpoints
{
    /// <summary>POST — Create integration (no sub-path, just the base prefix)</summary>
    public const string Create = "/";

    /// <summary>DELETE — Remove integration: /{integrationId}</summary>
    public const string Remove = "/{integrationId:guid}";

    /// <summary>PUT — Toggle integration status: /{integrationId}/status</summary>
    public const string ToggleStatus = "/{integrationId:guid}/status";

    /// <summary>PUT — Change admin profile: /{integrationId}/admin</summary>
    public const string ChangeAdmin = "/{integrationId:guid}/admin";
}

/// <summary>
/// Extension methods for mapping Linbik integration webhook endpoints.
/// These endpoints receive lifecycle notifications from Linbik platform
/// when main services create, remove, toggle, or change admin profiles for integrations.
/// 
/// Usage:
/// <code>
/// // In Program.cs
/// builder.Services.AddLinbikIntegrationHandler&lt;MyIntegrationHandler&gt;();
/// 
/// var app = builder.Build();
/// app.MapLinbikIntegrationEndpoints(); // defaults to /api/external
/// // or
/// app.MapLinbikIntegrationEndpoints("/custom/path");
/// </code>
/// </summary>
public static class IntegrationEndpointExtensions
{
    /// <summary>
    /// Register a custom integration handler that will process incoming integration events.
    /// The handler must implement <see cref="ILinbikIntegrationHandler"/>.
    /// For convenience, you can extend <see cref="LinbikIntegrationHandler"/> and override only the methods you need.
    /// </summary>
    public static IServiceCollection AddLinbikIntegrationHandler<THandler>(this IServiceCollection services)
        where THandler : class, ILinbikIntegrationHandler
    {
        services.AddScoped<ILinbikIntegrationHandler, THandler>();
        return services;
    }

    /// <summary>
    /// Register the default integration handler (logs events only).
    /// Override by calling <see cref="AddLinbikIntegrationHandler{THandler}"/> instead.
    /// </summary>
    public static IServiceCollection AddLinbikIntegrationHandler(this IServiceCollection services)
    {
        services.AddScoped<ILinbikIntegrationHandler, LinbikIntegrationHandler>();
        return services;
    }

    /// <summary>
    /// Maps the Linbik integration webhook endpoints.
    /// These endpoints are called by Linbik.App when integration lifecycle events occur.
    /// 
    /// Endpoints mapped (using <see cref="LinbikIntegrationEndpoints"/>):
    /// - POST   {basePath}/              → Integration created
    /// - DELETE {basePath}/{id}          → Integration removed
    /// - PUT    {basePath}/{id}/status   → Integration toggled (enabled/disabled)
    /// - PUT    {basePath}/{id}/admin    → Admin profile changed
    /// 
    /// All endpoints require LinbikS2S authentication by default.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="basePath">Base path for integration endpoints (default: /api/external)</param>
    /// <returns>A route group builder for further configuration</returns>
    public static RouteGroupBuilder MapLinbikIntegrationEndpoints(
        this IEndpointRouteBuilder endpoints,
        string basePath = "/api/external")
    {
        var group = endpoints.MapGroup(basePath)
            .WithTags("Linbik Integrations")
            .RequireAuthorization(policy =>
            {
                policy.AuthenticationSchemes = ["LinbikS2S"];
                policy.RequireAuthenticatedUser();
            });

        // POST {basePath}/ — Integration created
        group.MapPost(LinbikIntegrationEndpoints.Create, async (IntegrationEvent integrationEvent, ILinbikIntegrationHandler handler, ILogger<LinbikIntegrationHandler> logger) =>
        {
            try
            {
                integrationEvent.EventType = IntegrationEventType.Created;
                var result = await handler.OnIntegrationCreatedAsync(integrationEvent);

                return result.IsSuccess
                    ? Results.Ok(new { success = true, message = result.Message ?? "Integration registered" })
                    : Results.BadRequest(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling integration created event for {IntegrationId}", integrationEvent.IntegrationId);
                return Results.StatusCode(500);
            }
        })
        .WithName("LinbikIntegrationCreated")
        .WithDescription("Called by Linbik when a main service registers an integration");

        // DELETE {basePath}/{integrationId} — Integration removed
        group.MapDelete(LinbikIntegrationEndpoints.Remove, async (Guid integrationId, HttpRequest request, ILinbikIntegrationHandler handler, ILogger<LinbikIntegrationHandler> logger) =>
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

                return result.IsSuccess
                    ? Results.Ok(new { success = true, message = result.Message ?? "Integration removed" })
                    : Results.BadRequest(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling integration removed event for {IntegrationId}", integrationId);
                return Results.StatusCode(500);
            }
        })
        .WithName("LinbikIntegrationRemoved")
        .WithDescription("Called by Linbik when a main service removes an integration");

        // PUT {basePath}/{integrationId}/status — Integration toggled
        group.MapPut(LinbikIntegrationEndpoints.ToggleStatus, async (Guid integrationId, IntegrationEvent integrationEvent, ILinbikIntegrationHandler handler, ILogger<LinbikIntegrationHandler> logger) =>
        {
            try
            {
                integrationEvent.IntegrationId = integrationId;
                integrationEvent.EventType = IntegrationEventType.Toggled;

                var result = await handler.OnIntegrationToggledAsync(integrationEvent);

                return result.IsSuccess
                    ? Results.Ok(new { success = true, message = result.Message ?? "Integration status updated" })
                    : Results.BadRequest(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling integration toggled event for {IntegrationId}", integrationId);
                return Results.StatusCode(500);
            }
        })
        .WithName("LinbikIntegrationToggled")
        .WithDescription("Called by Linbik when a main service toggles an integration");

        // PUT {basePath}/{integrationId}/admin — Admin profile changed
        group.MapPut(LinbikIntegrationEndpoints.ChangeAdmin, async (Guid integrationId, IntegrationEvent integrationEvent, ILinbikIntegrationHandler handler, ILogger<LinbikIntegrationHandler> logger) =>
        {
            try
            {
                integrationEvent.IntegrationId = integrationId;
                integrationEvent.EventType = IntegrationEventType.AdminChanged;

                var result = await handler.OnIntegrationAdminChangedAsync(integrationEvent);

                return result.IsSuccess
                    ? Results.Ok(new { success = true, message = result.Message ?? "Integration admin updated" })
                    : Results.BadRequest(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling integration admin changed event for {IntegrationId}", integrationId);
                return Results.StatusCode(500);
            }
        })
        .WithName("LinbikIntegrationAdminChanged")
        .WithDescription("Called by Linbik when a main service changes the admin profile");

        return group;
    }

    /// <summary>
    /// Maps integration endpoints without authentication requirement.
    /// Use this only for development/testing purposes.
    /// </summary>
    public static RouteGroupBuilder MapLinbikIntegrationEndpointsAnonymous(
        this IEndpointRouteBuilder endpoints,
        string basePath = "/api/external")
    {
        var group = MapLinbikIntegrationEndpoints(endpoints, basePath);
        group.AllowAnonymous();
        return group;
    }
}
