using System.Security.Claims;
using Linbik.Core.Attributes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace Linbik.Core.Extensions;

/// <summary>A flow selected by endpoint routing and verified locally, never by incoming headers.</summary>
public sealed record LinbikStandaloneFlow(string Flow);

/// <summary>Metadata on a concrete, flow-specific standalone route.</summary>
public sealed record LinbikStandaloneRoute(string Flow);

/// <summary>Tracks standalone middleware installation for startup validation.</summary>
public sealed class LinbikStandalonePipelineState
{
    /// <summary>Whether UseLinbikStandaloneFlows was registered on the mapping application.</summary>
    public bool MiddlewareInstalled { get; internal set; }
}

/// <summary>Explicit direct-host mode, separate from trusted gateway downstream mode.</summary>
public static class LinbikStandaloneExtensions
{
    private const string PipelineStateKey = "Linbik.Standalone.PipelineState";

    private static LinbikStandalonePipelineState PipelineState(IApplicationBuilder app)
    {
        if (!app.Properties.TryGetValue(PipelineStateKey, out var value))
            app.Properties[PipelineStateKey] = value = new LinbikStandalonePipelineState();
        if (value is LinbikStandalonePipelineState state) return state;
        throw new InvalidOperationException("Application.Properties['Linbik.Standalone.PipelineState'] contains an incompatible value. This key is owned by Linbik; remove custom writes to it before calling UseLinbikStandaloneFlows or MapLinbikStandaloneEndpoints.");
    }

    /// <summary>
    /// Maps application API endpoints in Self, Delegated and Application forms.
    /// Call this instead of mapping the same endpoints separately.
    /// Original routes must start with /api. Linbik system routes remain Self-only.
    /// </summary>
    public static IEndpointRouteBuilder MapLinbikStandaloneEndpoints(
        this IEndpointRouteBuilder app, Action<IEndpointRouteBuilder> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var isolated = new IsolatedRoutes(app);
        map(isolated);
        foreach (var source in isolated.DataSources)
            app.DataSources.Add(new FlowDataSource(source, app is IApplicationBuilder pipeline ? PipelineState(pipeline) : null));
        return app;
    }

    /// <summary>
    /// Install after UseAuthentication and before UseAuthorization; this method installs routing. Register authentication
    /// schemes through normal Linbik modules. Do not use in gateway downstream services.
    /// </summary>
    public static IApplicationBuilder UseLinbikStandaloneFlows(this IApplicationBuilder app)
    {
        var state = PipelineState(app);
        if (state.MiddlewareInstalled)
            throw new InvalidOperationException("UseLinbikStandaloneFlows was registered twice. Keep one call: app.UseAuthentication(); app.UseLinbikStandaloneFlows(); app.UseAuthorization();");
        state.MiddlewareInstalled = true;
        app.Use(async (context, next) =>
        {
            foreach (var key in context.Request.Headers.Keys
                .Where(k => k.StartsWith("Linbik-", StringComparison.OrdinalIgnoreCase)).ToArray())
                context.Request.Headers.Remove(key);
            await next(context);
        });
        app.UseRouting();
        app.Use(async (context, next) =>
        {
            var route = context.GetEndpoint()?.Metadata.GetMetadata<LinbikStandaloneRoute>();
            if (route is null) { await next(context); return; }
            var authorization = context.GetEndpoint()!.Metadata.GetOrderedMetadata<IAuthorizeData>();
            var explicitPolicies = context.GetEndpoint()!.Metadata.GetOrderedMetadata<AuthorizationPolicy>();
            if (authorization.Count > 0 || explicitPolicies.Count > 0 || context.RequestServices.GetService<IAuthorizationPolicyProvider>() is not null)
            {
                var policyProvider = context.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>();
                var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorization, explicitPolicies);
                var selectedScheme = LinbikDefaults.SchemeForFlow(route.Flow);
                if (policy?.AuthenticationSchemes.Any(s => s != selectedScheme) == true)
                    throw new InvalidOperationException("Standalone route authorization must not select another authentication scheme. Remove AuthenticationSchemes from RequireAuthorization/[Authorize]; use flow metadata and scheme-neutral role/permission policies. Expected order: app.UseAuthentication(); app.UseLinbikStandaloneFlows(); app.UseAuthorization();");
            }
            var rule = context.GetEndpoint()!.Metadata.GetMetadata<LFlowAuthorizeAttribute>();
            var isPublic = rule?.AllowedFlows.Contains("*", StringComparer.Ordinal) == true;
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            if (!isPublic)
            {
                var scheme = LinbikDefaults.SchemeForFlow(route.Flow)!;
                var provider = context.RequestServices.GetService(typeof(IAuthenticationSchemeProvider))
                    as IAuthenticationSchemeProvider;
                if (provider is null || await provider.GetSchemeAsync(scheme) is null)
                    throw new InvalidOperationException($"Standalone flow '{route.Flow}' requires authentication scheme '{scheme}'. Register Self with AddLinbikJwtAuth/AddLinbikPasetoAuth or Delegated/Application with AddLinbikServer before builder.Build(). Expected order: app.UseAuthentication(); app.UseLinbikStandaloneFlows(); app.UseAuthorization();");
                var result = await context.AuthenticateAsync(scheme);
                if (!result.Succeeded || result.Principal is null)
                { context.Response.StatusCode = 401; return; }
                context.User = result.Principal;
            }
            context.Features.Set(new LinbikStandaloneFlow(route.Flow));
            if (context.User.Identity?.IsAuthenticated == true)
                context.Request.Headers[LinbikDefaults.HeaderFlow] = route.Flow;
            await next(context);
        });
        return app;
    }

    private sealed class IsolatedRoutes(IEndpointRouteBuilder parent) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider => parent.ServiceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IApplicationBuilder CreateApplicationBuilder() => parent.CreateApplicationBuilder();
    }

    private sealed class FlowDataSource(EndpointDataSource source, LinbikStandalonePipelineState? pipelineState) : EndpointDataSource
    {
        public override IChangeToken GetChangeToken() => source.GetChangeToken();
        public override IReadOnlyList<Endpoint> Endpoints
        {
            get
            {
                var endpoints = new List<Endpoint>();
                foreach (var endpoint in source.Endpoints)
                {
                    if (endpoint is not RouteEndpoint original)
                        throw new InvalidOperationException("Standalone routing requires RouteEndpoint mappings. Map routes with MapGet/MapPost/MapControllers inside MapLinbikStandaloneEndpoints; custom EndpointDataSource entries must produce RouteEndpoint instances.");
                    var system = original.Metadata.GetOrderedMetadata<ITagsMetadata>()
                        .Any(t => t.Tags.Any(tag => tag is "Linbik" or "Linbik System"));
                    if (system) { endpoints.Add(original); continue; }
                    var path = original.RoutePattern.RawText ?? "";
                    if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) && path != "/api")
                        throw new InvalidOperationException($"Standalone API route '{path}' must start with /api. Example: [LinbikSlice(\"/api/orders\")]. Map these endpoints only inside MapLinbikStandaloneEndpoints; the /api/apps and /api/delegated prefixes are added by Linbik.");
                    var rule = original.Metadata.GetMetadata<LFlowAuthorizeAttribute>();
                    foreach (var (flow, prefix) in new[]
                    {
                        ("Self", "/api"), ("Delegated", "/api/delegated"), ("Application", "/api/apps")
                    })
                    {
                        if (rule is { AllowedFlows.Length: > 0 } &&
                            !rule.AllowedFlows.Contains("*") &&
                            !rule.AllowedFlows.Contains(flow, StringComparer.OrdinalIgnoreCase))
                            continue;
                        RequestDelegate guarded = async context =>
                        {
                            var verified = context.Features.Get<LinbikStandaloneFlow>();
                            if (verified?.Flow != flow)
                                throw new InvalidOperationException("Standalone routes were mapped without verified flow middleware. Add app.UseLinbikStandaloneFlows() before endpoint execution. Expected order: app.UseAuthentication(); app.UseLinbikStandaloneFlows(); app.UseAuthorization(); Map business routes only inside app.MapLinbikStandaloneEndpoints(routes => routes.MapLinbikSlices()).");
                            await original.RequestDelegate!(context);
                        };
                        var builder = new RouteEndpointBuilder(guarded,
                            RoutePatternFactory.Parse(prefix + path[4..], new RouteValueDictionary(original.RoutePattern.Defaults), null),
                            original.Order) { DisplayName = original.DisplayName + " [" + flow + "]" };
                        foreach (var metadata in original.Metadata)
                        {
                            if (metadata is IEndpointNameMetadata named)
                                builder.Metadata.Add(new EndpointNameMetadata(named.EndpointName + "_" + flow));
                            else builder.Metadata.Add(metadata);
                        }
                        builder.Metadata.Add(new LinbikStandaloneRoute(flow));
                        if (pipelineState is not null) builder.Metadata.Add(pipelineState);
                        endpoints.Add(builder.Build());
                    }
                }
                return endpoints;
            }
        }
    }
}
