using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;
using Linbik.YARP.Configuration;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Linbik.YARP.OpenApi;

public static class LinbikDelegatedOpenApiExtensions
{
    private static readonly ConditionalWeakTable<OpenApiOptions, object> ConfiguredOptions = new();
    /// <summary>Register the shared authenticated document cache. Does not map any endpoints.</summary>
    public static IServiceCollection AddLinbikDelegatedOpenApi(this IServiceCollection services,
        Action<LinbikDelegatedOpenApiOptions>? configure = null)
    {
        var options = services.AddOptions<LinbikDelegatedOpenApiOptions>();
        if (configure is not null) options.Configure(configure);
        options.Validate(o => o.RefreshInterval >= TimeSpan.FromSeconds(10), "RefreshInterval must be at least 10 seconds.").ValidateOnStart();
        services.AddHttpClient(DelegatedDocumentCache.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.TryAddSingleton<DelegatedDocumentCache>();
        services.AddHostedService(sp => sp.GetRequiredService<DelegatedDocumentCache>());
        return services;
    }

    /// <summary>Import delegated operations into this document only. Host controls publication and authorization.</summary>
    public static OpenApiOptions AddLinbikDelegatedDocuments(this OpenApiOptions options)
    {
        lock (ConfiguredOptions)
        {
            if (!ConfiguredOptions.TryGetValue(options, out _))
            {
                options.AddDocumentTransformer<DelegatedDocumentTransformer>();
                ConfiguredOptions.Add(options, new object());
            }
        }
        return options;
    }
}

public sealed class DelegatedDocumentTransformer(DelegatedDocumentCache cache,
    IOptions<YARPOptions> options, ILogger<DelegatedDocumentTransformer> logger) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var sources = await cache.GetDocumentsAsync(cancellationToken);
        if (sources.Count == 0) return;
        var json = JsonNode.Parse(await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1, cancellationToken))!.AsObject();
        foreach (var (package, source) in sources)
        {
            try
            {
                var candidate = DelegatedDocumentMerger.Merge(json, JsonNode.Parse(source)!.AsObject(), package,
                    options.Value.IntegrationServices[package].SourcePath,
                    options.Value.IntegrationServices[package].TargetPath);
                var parsed = DelegatedDocumentReader.Read(candidate);
                // Commit only a successfully parsed merge. A failed import must not break
                // the host's own OpenAPI endpoint or discard earlier successful imports.
                document.Paths = parsed.Paths;
                document.Components = parsed.Components;
                json = candidate;
            }
            catch (Exception ex)
            { logger.LogWarning(ex, "Skipping conflicting delegated document for {Package}", package); }
        }
    }
}
