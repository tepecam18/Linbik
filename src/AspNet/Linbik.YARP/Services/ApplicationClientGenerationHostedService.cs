using System.Text;
using Linbik.YARP.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSwag;
using NSwag.CodeGeneration.CSharp;

namespace Linbik.YARP.Services;

/// <summary>
/// Startup hosted service that regenerates typed Application (S2S) clients with NSwag.
/// For every <see cref="IntegrationServiceOptions"/> entry with a non-empty
/// <see cref="IntegrationServiceOptions.DocumentPath"/>, probes
/// <c>{TargetBaseUrl}{DocumentPath}</c>. When the OpenAPI document is reachable, the client
/// source file is regenerated on disk (<see cref="YARPOptions.GeneratedClientOutputDirectory"/>).
/// When it is not reachable (network error, timeout, non-success status, invalid document),
/// generation is skipped for that run and the previously generated client keeps being used as-is —
/// this service never deletes or blanks out an existing generated file.
/// </summary>
public sealed class ApplicationClientGenerationHostedService(
    IHttpClientFactory httpClientFactory,
    IOptions<YARPOptions> options,
    IHostEnvironment environment,
    ILogger<ApplicationClientGenerationHostedService> logger) : IHostedService
{
    /// <summary>Named HttpClient used solely to probe/download integration services' OpenAPI documents.</summary>
    public const string ProbeClientName = "Linbik.YARP.NSwagProbe";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var yarpOptions = options.Value;
        var servicesToGenerate = yarpOptions.IntegrationServices
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value.DocumentPath))
            .ToList();

        if (servicesToGenerate.Count == 0)
            return;

        foreach (var (packageName, serviceConfig) in servicesToGenerate)
        {
            try
            {
                await RegenerateClientAsync(packageName, serviceConfig, yarpOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                // Any unexpected failure must never break gateway startup — fall back to the
                // existing generated client (if any) and move on to the next service.
                logger.LogWarning(ex,
                    "Application client regeneration failed unexpectedly for {PackageName}; keeping existing generated client",
                    packageName);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RegenerateClientAsync(
        string packageName,
        IntegrationServiceOptions serviceConfig,
        YARPOptions yarpOptions,
        CancellationToken cancellationToken)
    {
        var documentUrl = $"{serviceConfig.TargetBaseUrl.TrimEnd('/')}/{serviceConfig.DocumentPath!.TrimStart('/')}";

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(yarpOptions.DocumentCheckTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        string json;
        try
        {
            var client = httpClientFactory.CreateClient(ProbeClientName);
            using var response = await client.GetAsync(documentUrl, linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "OpenAPI document for {PackageName} not reachable ({StatusCode}) at {Url}; keeping existing generated Application client",
                    packageName, (int)response.StatusCode, documentUrl);
                return;
            }

            json = await response.Content.ReadAsStringAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Either our own probe timeout, or the underlying HttpClient.Timeout, fired — both mean unreachable.
            logger.LogInformation(
                "OpenAPI document probe timed out for {PackageName} at {Url}; keeping existing generated Application client",
                packageName, documentUrl);
            return;
        }
        catch (HttpRequestException ex)
        {
            logger.LogInformation(ex,
                "OpenAPI document unreachable for {PackageName} at {Url}; keeping existing generated Application client",
                packageName, documentUrl);
            return;
        }

        OpenApiDocument document;
        try
        {
            document = await OpenApiDocument.FromJsonAsync(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "OpenAPI document for {PackageName} at {Url} could not be parsed; keeping existing generated Application client",
                packageName, documentUrl);
            return;
        }

        var className = $"{ToPascalCase(packageName)}ApplicationClient";
        var settings = new CSharpClientGeneratorSettings
        {
            ClassName = className,
            GenerateClientInterfaces = true,
            UseBaseUrl = false,
            InjectHttpClient = true,
            CSharpGeneratorSettings =
            {
                Namespace = "Linbik.YARP.Generated"
            }
        };

        var generator = new CSharpClientGenerator(document, settings);
        var generatedCode = generator.GenerateFile();

        var outputDirectory = Path.Combine(environment.ContentRootPath, yarpOptions.GeneratedClientOutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, $"{className}.g.cs");

        if (File.Exists(outputPath) &&
            string.Equals(await File.ReadAllTextAsync(outputPath, cancellationToken), generatedCode, StringComparison.Ordinal))
        {
            logger.LogDebug("Application client for {PackageName} already up to date at {Path}", packageName, outputPath);
            return;
        }

        await File.WriteAllTextAsync(outputPath, generatedCode, Encoding.UTF8, cancellationToken);
        logger.LogInformation("Regenerated Application client {ClassName} for {PackageName} at {Path}",
            className, packageName, outputPath);
    }

    private static string ToPascalCase(string packageName)
    {
        var parts = packageName.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            builder.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
                builder.Append(part[1..]);
        }

        return builder.Length > 0 ? builder.ToString() : packageName;
    }
}
