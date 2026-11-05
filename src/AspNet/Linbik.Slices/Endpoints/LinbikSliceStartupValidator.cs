using Linbik.Core;
using Linbik.Core.Attributes;
using Linbik.Core.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Linbik.Slices.Endpoints;

internal sealed class LinbikSliceStartupValidator(IServiceProvider services) : IHostedLifecycleService
{
    // Minimal API endpoint data sources are attached while the web host starts.
    // Validate after all hosted services have started, before host startup succeeds.
    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        var source = services.GetService<EndpointDataSource>();
        if (source is null) return;
        var provider = services.GetService<IAuthenticationSchemeProvider>();
        var registered = provider is null ? new HashSet<string>(StringComparer.Ordinal) :
            (await provider.GetAllSchemesAsync()).Select(s => s.Name).ToHashSet(StringComparer.Ordinal);
        var policyProvider = services.GetService<IAuthorizationPolicyProvider>();
        var errors = new List<string>();
        foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var standalone = endpoint.Metadata.GetMetadata<LinbikStandaloneRoute>();
            if (standalone is null && endpoint.Metadata.GetMetadata<LinbikSliceAuthenticationMetadata>() is null) continue;
            var rule = endpoint.Metadata.GetMetadata<LFlowAuthorizeAttribute>();
            var path = endpoint.RoutePattern.RawText ?? endpoint.DisplayName ?? "unknown endpoint";
            if (standalone is not null && endpoint.Metadata.GetMetadata<LinbikStandalonePipelineState>() is { MiddlewareInstalled: false })
                errors.Add($"'{path}' was mapped with MapLinbikStandaloneEndpoints but UseLinbikStandaloneFlows is missing. Add app.UseAuthentication(); app.UseLinbikStandaloneFlows(); app.UseAuthorization(); before endpoint execution. Do not also map these business routes outside MapLinbikStandaloneEndpoints.");
            var isPublic = rule?.AllowedFlows.Contains("*", StringComparer.Ordinal) == true;
            if (!isPublic)
            {
                var flows = standalone is not null ? new[] { standalone.Flow } :
                    rule?.AllowedFlows is { Length: > 0 } allowed ? allowed : new[] { "Self", "Delegated", "Application" };
                var schemes = flows.Select(LinbikDefaults.SchemeForFlow).Where(s => s is not null).ToArray();
                // A non-standalone LAuthorizeFlow endpoint intentionally accepts ANY of its allowed schemes.
                if (!schemes.Any(s => registered.Contains(s!)))
                    errors.Add($"'{path}' allows flow(s) [{string.Join(", ", flows)}] but none of their authentication schemes [{string.Join(", ", schemes)}] are registered. Register Self with AddLinbikJwtAuth/AddLinbikPasetoAuth, or Delegated/Application with AddLinbikServer, before builder.Build(). Example: builder.Services.AddLinbik(...).AddLinbikPasetoAuth(...); Use [LFlow] only behind a trusted gateway; do not replace this with [LFlowPublic] to bypass authentication.");
            }
            if (standalone is not null && policyProvider is not null)
            {
                var policy = await AuthorizationPolicy.CombineAsync(policyProvider,
                    endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());
                var scheme = LinbikDefaults.SchemeForFlow(standalone.Flow);
                if (policy?.AuthenticationSchemes.Any(s => s != scheme) == true)
                    errors.Add($"'{path}' selects another authentication scheme in an authorization policy. Standalone flow '{standalone.Flow}' selects '{scheme}'. Remove AuthenticationSchemes from RequireAuthorization/[Authorize] and keep scheme-neutral role/permission requirements. Expected order: app.UseAuthentication(); app.UseLinbikStandaloneFlows(); app.UseAuthorization();");
            }
        }
        if (errors.Count > 0)
            throw new InvalidOperationException("Linbik slice startup validation failed:\n" + string.Join("\n", errors));
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
