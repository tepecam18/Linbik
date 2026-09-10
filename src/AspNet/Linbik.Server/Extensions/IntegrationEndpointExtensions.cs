using Linbik.Core;
using Linbik.Core.Attributes;
using Linbik.Core.Responses;
using Linbik.Server.Interfaces;
using Linbik.Server.Models;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Linbik.Server.Extensions;

/// <summary>
/// Well-known endpoint sub-paths for Linbik integration lifecycle events.
/// These must match the paths used by Linbik.App's IntegrationNotificationService.
/// 
/// Full endpoint = {BaseUrl}{IntegrationPath}{SubPath}
/// Default IntegrationPath = "/api/Linbik"
/// </summary>
public static class LinbikIntegrationEndpoints
{
    /// <summary>POST — Create integration (no sub-path; matches sender's empty Create path).</summary>
    public const string Create = "";

    /// <summary>DELETE — Remove integration: /{integrationId}</summary>
    public const string Remove = "/{integrationId:guid}";

    /// <summary>PUT — Toggle integration status: /{integrationId}/status</summary>
    public const string ToggleStatus = "/{integrationId:guid}/status";

    /// <summary>PUT — Change admin profile: /{integrationId}/admin</summary>
    public const string ChangeService = "/{integrationId:guid}/service";
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
/// app.MapLinbikIntegrationEndpoints(); // defaults to /api/Linbik
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
        // AddLinbikServer() default handler'ı TryAddScoped ile kaydetmiş olabilir.
        // Önce mevcut kaydı kaldırıp, kullanıcının istediği THandler'ı tekil kayıt olarak ekliyoruz.
        services.RemoveAll<ILinbikIntegrationHandler>();
        services.AddScoped<ILinbikIntegrationHandler, THandler>();
        return services;
    }

    /// <summary>
    /// Register the default integration handler (logs events only).
    /// Override by calling <see cref="AddLinbikIntegrationHandler{THandler}"/> instead.
    /// Not: <c>AddLinbikServer()</c> zaten default handler'ı Optional DI ile (TryAdd) kaydeder;
    /// bu metoda yalnızca explicit kayıt gerektiğinde ihtiyaç vardır.
    /// </summary>
    public static IServiceCollection AddLinbikIntegrationHandler(this IServiceCollection services)
    {
        services.TryAddScoped<ILinbikIntegrationHandler, LinbikIntegrationHandler>();
        return services;
    }

    /// <summary>
    /// Maps the Linbik integration webhook route group and its endpoints, without applying
    /// any authorization. Used by both <see cref="MapLinbikIntegrationEndpoints"/> (which adds
    /// the platform-role policy) and <see cref="MapLinbikIntegrationEndpointsAnonymous"/> (which adds none).
    ///
    /// Endpoints mapped (using <see cref="LinbikIntegrationEndpoints"/>):
    /// - POST   {basePath}/              → Integration created
    /// - DELETE {basePath}/{id}          → Integration removed
    /// - PUT    {basePath}/{id}/status   → Integration toggled (enabled/disabled)
    /// - PUT    {basePath}/{id}/admin    → Admin profile changed
    /// </summary>
    private static RouteGroupBuilder MapLinbikIntegrationRoutes(
        this IEndpointRouteBuilder endpoints,
        string basePath)
    {
        var group = endpoints.MapGroup(basePath)
            .WithTags("Linbik System");

        // POST {basePath}/ — Integration created
        group.MapPost(LinbikIntegrationEndpoints.Create, IntegrationEventEndpoints.CreateAsync)
        .WithName("LinbikIntegrationCreated")
        .WithDescription("Called by Linbik when a main service registers an integration");

        // DELETE {basePath}/{integrationId} — Integration removed
        group.MapDelete(LinbikIntegrationEndpoints.Remove, IntegrationEventEndpoints.RemoveAsync)
        .WithName("LinbikIntegrationRemoved")
        .WithDescription("Called by Linbik when a main service removes an integration");

        // PUT {basePath}/{integrationId}/status — Integration toggled
        group.MapPut(LinbikIntegrationEndpoints.ToggleStatus, IntegrationEventEndpoints.ToggleStatusAsync)
        .WithName("LinbikIntegrationToggled")
        .WithDescription("Called by Linbik when a main service toggles an integration");

        // PUT {basePath}/{integrationId}/admin — Admin profile changed
        group.MapPut(LinbikIntegrationEndpoints.ChangeAdmin, IntegrationEventEndpoints.ChangeAdminAsync)
        .WithName("LinbikIntegrationAdminChanged")
        .WithDescription("Called by Linbik when a main service changes the admin profile");

        group.MapPut(LinbikIntegrationEndpoints.ChangeService, IntegrationEventEndpoints.ChangeServiceAsync)
            .WithName("LinbikIntegrationServiceChanged");
        return group;
    }

    /// <summary>
    /// Maps the Linbik integration webhook endpoints and applies the platform-role authorization policy.
    /// These endpoints are called by Linbik.App when integration lifecycle events occur.
    /// All endpoints require LinbikApplication authentication with the "Linbik" role claim by default.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="basePath">Base path for integration endpoints (default: /api/Linbik)</param>
    /// <returns>A route group builder for further configuration</returns>
    public static RouteGroupBuilder MapLinbikIntegrationEndpoints(
        this IEndpointRouteBuilder endpoints,
        string basePath = "/api/Linbik/")
    {
        var group = endpoints.MapLinbikIntegrationRoutes(basePath);

        group.RequireAuthorization(policy =>
        {
            policy.AuthenticationSchemes = [LinbikDefaults.ApplicationScheme];
            policy.RequireAuthenticatedUser();
            // Not: "role" claim'i ham claim tipiyle (ClaimTypes.Role değil) geldiğinden
            // RequireRole/Roles= yerine RequireClaim kullanılmalı — bkz. PasetoBearerHandler.
            policy.RequireClaim("role", "Linbik");
        });

        return group;
    }

    /// <summary>
    /// Gateway'in, authenticate olmuş isteğin "role" claim'ini downstream servise ilettiği
    /// header adı (bkz. <c>LinbikClaimsHeaderTransform</c>: <c>Linbik-{ClaimType}</c>).
    /// </summary>
    private const string HeaderRole = "Linbik-role";

    /// <summary>
    /// Linbik platformunun kendi application/webhook çağrılarını tanımlayan role claim/header değeri.
    /// </summary>
    private const string PlatformRole = "Linbik";

    /// <summary>
    /// Maps the Linbik integration webhook endpoints for services that sit behind the API Gateway
    /// and cannot re-validate a bearer token locally (anahtar materyali yalnızca Gateway'de bulunur).
    /// Default yetkilendirme, Gateway'in authenticate ettikten sonra ilettiği header'ları kontrol eder:
    /// <c>Linbik-Flow: Application</c> + <c>Linbik-role: Linbik</c> (bkz. <c>LinbikClaimsHeaderTransform</c>,
    /// <c>LinbikHeaderSanitizationMiddleware</c> — bu header'lar yalnızca Gateway authenticate ettikten
    /// sonra yazılır, client tarafından spoof edilemez).
    ///
    /// Farklı bir yetkilendirme istiyorsanız <paramref name="configureAuthorization"/> ile
    /// override edebilirsiniz, ör. eski token-tabanlı doğrulamayı geri getirmek için:
    /// <code>
    /// app.MapLinbikIntegrationEndpointsGateway(configureAuthorization: group =>
    ///     group.RequireAuthorization(policy =>
    ///     {
    ///         policy.AuthenticationSchemes = [LinbikDefaults.ApplicationScheme];
    ///         policy.RequireAuthenticatedUser();
    ///         policy.RequireClaim("role", "Linbik");
    ///     }));
    /// </code>
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="basePath">Base path for integration endpoints (default: /api/Linbik)</param>
    /// <param name="configureAuthorization">
    /// Verilirse default header kontrolü (Linbik-Flow + Linbik-role) uygulanmaz; bunun yerine
    /// bu delegate route group üzerinde çağrılarak yetkilendirmeyi tamamen override eder.
    /// </param>
    /// <returns>A route group builder for further configuration</returns>
    public static RouteGroupBuilder MapLinbikIntegrationEndpointsGateway(
        this IEndpointRouteBuilder endpoints,
        string basePath = "/api/Linbik/",
        Action<RouteGroupBuilder>? configureAuthorization = null)
    {
        var group = endpoints.MapLinbikIntegrationRoutes(basePath);

        if (configureAuthorization is not null)
        {
            configureAuthorization(group);
        }
        else
        {
            group.AddEndpointFilter(RequireLinbikPlatformHeaders);
        }

        return group;
    }

    /// <summary>
    /// Default yetkilendirme filtresi: <c>Linbik-Flow: Application</c> ve <c>Linbik-role: Linbik</c>
    /// header'larını zorunlu kılar. Her ikisi de Gateway tarafından, isteği authenticate ettikten
    /// sonra yazılır ve <c>LinbikHeaderSanitizationMiddleware</c> tarafından client girdisinden
    /// önceden temizlenmiş olur — bkz. <c>LinbikClaimsHeaderTransform</c>.
    /// </summary>
    private static async ValueTask<object?> RequireLinbikPlatformHeaders(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        var flowDecision = LFlowGate.Evaluate(
            request.Headers[LinbikDefaults.HeaderFlow],
            [LinbikDefaults.Flows.Application]);

        if (!flowDecision.Allowed)
        {
            return Results.Json(
                new LBaseResponse<object>(title: flowDecision.Title, message: flowDecision.Message, isSuccess: false),
                statusCode: flowDecision.StatusCode);
        }

        var roleValues = request.Headers[HeaderRole];
        if (roleValues.Count != 1 ||
            !string.Equals(roleValues.ToString().Trim(), PlatformRole, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new LBaseResponse<object>(
                    title: "forbidden_role",
                    message: $"Header '{HeaderRole}' must equal '{PlatformRole}'.",
                    isSuccess: false),
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }

    /// <summary>
    /// Maps integration endpoints without any authorization policy.
    /// Use this only for development/testing purposes.
    /// </summary>
    public static RouteGroupBuilder MapLinbikIntegrationEndpointsAnonymous(
        this IEndpointRouteBuilder endpoints,
        string basePath = "/api/Linbik")
    {
        return endpoints.MapLinbikIntegrationRoutes(basePath);
    }
}
