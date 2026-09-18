namespace Linbik.YARP.OpenApi;

public sealed class LinbikDelegatedOpenApiOptions
{
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(15);
    /// <summary>Last successful documents survive restarts. Null disables disk caching.</summary>
    public string? CacheDirectory { get; set; } = ".linbik/openapi";
}
