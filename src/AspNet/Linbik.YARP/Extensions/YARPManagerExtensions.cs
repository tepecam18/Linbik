using Linbik.Core.Interfaces;
using Linbik.YARP.Configuration;
using Linbik.YARP.Interfaces;
using Linbik.YARP.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Linbik.YARP.Extensions;

/// <summary>
/// Extension methods for configuring Linbik YARP reverse proxy
/// </summary>
public static class YARPManagerExtensions
{
    /// <summary>
    /// Add Linbik proxy services with custom options
    /// </summary>
    public static ILinbikBuilder AddProxy(this ILinbikBuilder builder, Action<List<YARPOptions>> configureOptions)
    {
        builder.Services.Configure(configureOptions);

        var optionsInstance = new List<YARPOptions>();
        configureOptions(optionsInstance);

        AddCommonYarpServices(builder.Services, optionsInstance);

        return builder;
    }

    /// <summary>
    /// Add Linbik proxy services from configuration
    /// </summary>
    public static ILinbikBuilder AddProxy(this ILinbikBuilder builder)
    {
        var serviceProvider = builder.Services.BuildServiceProvider();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        builder.Services.Configure<List<YARPOptions>>(configuration.GetSection("Linbik:Yarp"));

        var yarpOptions = configuration.GetSection("Linbik:Yarp").Get<List<YARPOptions>>();

        if (yarpOptions is not null)
            AddCommonYarpServices(builder.Services, yarpOptions);

        return builder;
    }

    private static void AddCommonYarpServices(IServiceCollection services, List<YARPOptions> yarpOptions)
    {
        services.AddSingleton<ITokenProvider, MultiJwtTokenProvider>();
        services.AddHttpClient();

        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        foreach (var option in yarpOptions)
        {
            // Route configuration
            var route = new RouteConfig
            {
                RouteId = option.RouteId,
                ClusterId = option.ClusterId,
                Match = new RouteMatch { Path = option.PrefixPath + "/{**catch-all}" },
                AuthorizationPolicy = "LinbikProxyPolicy",
                Transforms = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string>
                    {
                        { "PathRemovePrefix", option.PrefixPath }
                    },
                    new Dictionary<string, string>
                    {
                        { "PathPrefix", "/api" }
                    }
                }
            };
            routes.Add(route);

            // Cluster configuration
            foreach (var cluster in option.Clusters)
            {
                var clusterConfig = new ClusterConfig
                {
                    ClusterId = option.ClusterId,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        { cluster.Name, new DestinationConfig { Address = cluster.Address } }
                    }
                };
                clusters.Add(clusterConfig);
            }
        }

        services.AddReverseProxy()
            .LoadFromMemory(routes, clusters)
            .AddTransforms(builderContext =>
            {
                builderContext.AddRequestTransform(async transformContext =>
                {
                    // Get user profile from auth service
                    var authService = transformContext.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                    var userProfile = await authService.GetUserProfileAsync(transformContext.HttpContext);
                    var userId = userProfile?.UserId.ToString() ?? string.Empty;

                    // Add user ID header
                    if (!string.IsNullOrEmpty(userId))
                    {
                        transformContext.ProxyRequest.Headers.Add("X-User-Id", userId);
                    }

                    // Get integration token from cookie if available
                    var baseUrl = transformContext.DestinationPrefix;
                    if (!string.IsNullOrEmpty(baseUrl))
                    {
                        var option = yarpOptions.FirstOrDefault(x =>
                            x.Clusters.Any(c => c.Address.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase)));

                        if (option != null && !string.IsNullOrEmpty(option.IntegrationPackageName))
                        {
                            var tokenProvider = transformContext.HttpContext.RequestServices.GetRequiredService<ITokenProvider>();
                            var token = await tokenProvider.GetIntegrationTokenAsync(option.IntegrationPackageName);

                            if (!string.IsNullOrEmpty(token))
                            {
                                transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                            }
                        }
                    }
                });
            });
    }
}


