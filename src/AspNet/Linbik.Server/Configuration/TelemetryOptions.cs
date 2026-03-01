namespace Linbik.Server.Configuration;

/// <summary>
/// Configuration options for OpenTelemetry integration
/// </summary>
public sealed class LinbikTelemetryOptions
{
    /// <summary>
    /// Enable OpenTelemetry tracing
    /// Default: true
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Enable OpenTelemetry metrics
    /// Default: true
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Service name for telemetry (used in traces and metrics)
    /// Default: "linbik-service"
    /// </summary>
    public string ServiceName { get; set; } = "linbik-service";

    /// <summary>
    /// Service version for telemetry
    /// Default: "1.0.0"
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// OTLP exporter endpoint (e.g., "http://localhost:4317" for gRPC, "http://localhost:4318" for HTTP)
    /// Leave empty to disable OTLP export
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// OTLP exporter protocol: "grpc" or "http"
    /// Default: "grpc"
    /// </summary>
    public string OtlpProtocol { get; set; } = "grpc";

    /// <summary>
    /// Enable console exporter for development
    /// Default: false
    /// </summary>
    public bool EnableConsoleExporter { get; set; } = false;

    /// <summary>
    /// Sample ratio for traces (0.0 to 1.0)
    /// Default: 1.0 (all traces)
    /// </summary>
    public double TraceSampleRatio { get; set; } = 1.0;

    /// <summary>
    /// Enable ASP.NET Core instrumentation
    /// Default: true
    /// </summary>
    public bool EnableAspNetCoreInstrumentation { get; set; } = true;

    /// <summary>
    /// Enable HTTP client instrumentation
    /// Default: true
    /// </summary>
    public bool EnableHttpClientInstrumentation { get; set; } = true;

    /// <summary>
    /// Custom activity source names to include in tracing
    /// </summary>
    public string[] AdditionalSources { get; set; } = [];

    /// <summary>
    /// Custom meter names to include in metrics
    /// </summary>
    public string[] AdditionalMeters { get; set; } = [];
}
