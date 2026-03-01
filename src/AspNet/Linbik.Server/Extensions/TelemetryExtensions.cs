using Linbik.Server.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Linbik.Server.Extensions;

/// <summary>
/// Extension methods for OpenTelemetry integration with Linbik services
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Custom activity source name for Linbik operations
    /// </summary>
    public const string LinbikActivitySource = "Linbik.Server";

    /// <summary>
    /// Custom meter name for Linbik metrics
    /// </summary>
    public const string LinbikMeterName = "Linbik.Server";

    /// <summary>
    /// Add OpenTelemetry tracing and metrics for Linbik services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Optional configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddLinbikTelemetry(
        this IServiceCollection services,
        Action<LinbikTelemetryOptions>? configureOptions = null)
    {
        var options = new LinbikTelemetryOptions();
        configureOptions?.Invoke(options);

        services.Configure<LinbikTelemetryOptions>(opt =>
        {
            opt.EnableTracing = options.EnableTracing;
            opt.EnableMetrics = options.EnableMetrics;
            opt.ServiceName = options.ServiceName;
            opt.ServiceVersion = options.ServiceVersion;
            opt.OtlpEndpoint = options.OtlpEndpoint;
            opt.OtlpProtocol = options.OtlpProtocol;
            opt.EnableConsoleExporter = options.EnableConsoleExporter;
            opt.TraceSampleRatio = options.TraceSampleRatio;
            opt.EnableAspNetCoreInstrumentation = options.EnableAspNetCoreInstrumentation;
            opt.EnableHttpClientInstrumentation = options.EnableHttpClientInstrumentation;
            opt.AdditionalSources = options.AdditionalSources;
            opt.AdditionalMeters = options.AdditionalMeters;
        });

        // Add OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(options.ServiceName, serviceVersion: options.ServiceVersion)
                .AddAttributes([
                    new("deployment.environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
                ]))
            .WithTracing(builder => ConfigureTracing(builder, options))
            .WithMetrics(builder => ConfigureMetrics(builder, options));

        return services;
    }

    /// <summary>
    /// Add OpenTelemetry from configuration
    /// </summary>
    public static IServiceCollection AddLinbikTelemetry(
        this IServiceCollection services,
        IConfigurationSection configuration)
    {
        var options = configuration.Get<LinbikTelemetryOptions>()
            ?? new LinbikTelemetryOptions();

        return services.AddLinbikTelemetry(opt =>
        {
            opt.EnableTracing = options.EnableTracing;
            opt.EnableMetrics = options.EnableMetrics;
            opt.ServiceName = options.ServiceName;
            opt.ServiceVersion = options.ServiceVersion;
            opt.OtlpEndpoint = options.OtlpEndpoint;
            opt.OtlpProtocol = options.OtlpProtocol;
            opt.EnableConsoleExporter = options.EnableConsoleExporter;
            opt.TraceSampleRatio = options.TraceSampleRatio;
            opt.EnableAspNetCoreInstrumentation = options.EnableAspNetCoreInstrumentation;
            opt.EnableHttpClientInstrumentation = options.EnableHttpClientInstrumentation;
            opt.AdditionalSources = options.AdditionalSources;
            opt.AdditionalMeters = options.AdditionalMeters;
        });
    }

    private static void ConfigureTracing(TracerProviderBuilder builder, LinbikTelemetryOptions options)
    {
        if (!options.EnableTracing)
            return;

        // Add Linbik activity source
        builder.AddSource(LinbikActivitySource);

        // Add additional sources
        foreach (var source in options.AdditionalSources)
        {
            builder.AddSource(source);
        }

        // ASP.NET Core instrumentation
        if (options.EnableAspNetCoreInstrumentation)
        {
            builder.AddAspNetCoreInstrumentation(opts =>
            {
                // Enrich spans with additional data
                opts.RecordException = true;
                opts.EnrichWithHttpRequest = (activity, request) =>
                {
                    activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
                    activity.SetTag("linbik.request_id", request.HttpContext.TraceIdentifier);
                };
                opts.EnrichWithHttpResponse = (activity, response) =>
                {
                    activity.SetTag("http.response_content_length", response.ContentLength);
                };
            });
        }

        // HTTP client instrumentation
        if (options.EnableHttpClientInstrumentation)
        {
            builder.AddHttpClientInstrumentation(opts =>
            {
                opts.RecordException = true;
            });
        }

        // Set sampler
        if (options.TraceSampleRatio < 1.0)
        {
            builder.SetSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio));
        }

        // OTLP exporter
        if (!string.IsNullOrEmpty(options.OtlpEndpoint))
        {
            builder.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                otlpOptions.Protocol = options.OtlpProtocol.Equals("http", StringComparison.OrdinalIgnoreCase)
                    ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                    : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            });
        }

        // Console exporter for development
        if (options.EnableConsoleExporter)
        {
            builder.AddConsoleExporter();
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder builder, LinbikTelemetryOptions options)
    {
        if (!options.EnableMetrics)
            return;

        // Add Linbik meter
        builder.AddMeter(LinbikMeterName);

        // Add additional meters
        foreach (var meter in options.AdditionalMeters)
        {
            builder.AddMeter(meter);
        }

        // ASP.NET Core metrics
        if (options.EnableAspNetCoreInstrumentation)
        {
            builder.AddAspNetCoreInstrumentation();
        }

        // HTTP client metrics
        if (options.EnableHttpClientInstrumentation)
        {
            builder.AddHttpClientInstrumentation();
        }

        // Runtime metrics
        builder.AddRuntimeInstrumentation();

        // OTLP exporter
        if (!string.IsNullOrEmpty(options.OtlpEndpoint))
        {
            builder.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                otlpOptions.Protocol = options.OtlpProtocol.Equals("http", StringComparison.OrdinalIgnoreCase)
                    ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                    : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            });
        }

        // Console exporter for development
        if (options.EnableConsoleExporter)
        {
            builder.AddConsoleExporter();
        }
    }
}
