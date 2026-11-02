using Linbik.Core.Attributes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Core.Extensions;

/// <summary>Publishes flow-specific OpenAPI documents without requiring a gateway.</summary>
public static class LinbikFlowDocumentExtensions
{
    /// <summary>
    /// Registers self, delegated and apps documents. Publish them with MapOpenApi().
    /// Configuration runs after flow filtering; compose ShouldInclude rather than replacing it.
    /// This registers documentation only and does not configure authentication or authorization.
    /// </summary>
    public static IServiceCollection AddLinbikFlowDocuments(
        this IServiceCollection services,
        Action<OpenApiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var (name, flow) in new[]
        {
            ("self", "Self"), ("delegated", "Delegated"), ("apps", "Application")
        })
        {
            services.AddOpenApi(name, options =>
            {
                options.AddLinbikFlowExtension();
                options.ShouldInclude = api =>
                {
                    var metadata = api.ActionDescriptor.EndpointMetadata;
                    var systemEndpoint = metadata.OfType<ITagsMetadata>()
                        .Any(tags => tags.Tags.Any(tag => tag is "Linbik System" or "Linbik"));
                    // Auth-manager controllers use the Linbik controller tag.
                    systemEndpoint |= api.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller)
                        && string.Equals(controller, "Linbik", StringComparison.OrdinalIgnoreCase);
                    if (systemEndpoint)
                        return name == "self";
                    var rule = api.ActionDescriptor.EndpointMetadata
                        .OfType<LFlowAuthorizeAttribute>().LastOrDefault();
                    return rule is null || rule.AllowedFlows.Length == 0 ||
                        rule.AllowedFlows.Contains(flow, StringComparer.OrdinalIgnoreCase);
                };
                configure?.Invoke(options);
            });
        }
        return services;
    }
}
