using Linbik.Core.Interfaces;
using Linbik.Server.Configuration;
using Linbik.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Linbik.Server.Extensions;

/// <summary>
/// Extension methods for configuring Linbik Server (integration service side)
/// </summary>
public static class ServerExtensions
{
    /// <summary>
    /// Add Linbik Server services with custom options
    /// Used by integration services to validate incoming JWT tokens
    /// </summary>
    public static ILinbikBuilder AddLinbikServer(this ILinbikBuilder builder, Action<ServerOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);
        AddCommonServerServices(builder.Services);
        return builder;
    }

    /// <summary>
    /// Add Linbik Server services from configuration
    /// </summary>
    public static ILinbikBuilder AddLinbikServer(this ILinbikBuilder builder)
    {
        var configuration = builder.Services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        builder.Services.Configure<ServerOptions>(configuration.GetSection("Linbik:Server"));
        AddCommonServerServices(builder.Services);
        return builder;
    }

    private static void AddCommonServerServices(IServiceCollection services)
    {
        // Add integration token validator
        services.AddSingleton<IntegrationTokenValidator>();
    }
}
