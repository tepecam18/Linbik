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

public static class YARPManagerExtensions
{
    public static ILinbikBuilder AddProxy(this ILinbikBuilder builder, Action<List<YARPOptions>> configureOptions)
    {
        builder.Services.Configure(configureOptions);

        var optionsInstance = new List<YARPOptions>();
        configureOptions(optionsInstance);

        // IOptions haline getiriyoruz.
        var yarpOptions = Options.Create(optionsInstance);

        AddCommonYarpServices(builder.Services, yarpOptions);

        return builder;
    }

    public static ILinbikBuilder AddProxy(this ILinbikBuilder builder)
    {
        var serviceProvider = builder.Services.BuildServiceProvider();
        var configuration = serviceProvider.GetService<IConfiguration>();

        builder.Services.Configure<List<YARPOptions>>(configuration.GetSection("Linbik:Yarp"));

        var yarpOptions = Options.Create(configuration.GetSection("Linbik:Yarp").Get<List<YARPOptions>>());

        if (yarpOptions.Value is not null)
            AddCommonYarpServices(builder.Services, yarpOptions);

        return builder;
    }

    // Ortak servis kayıtlarını tek bir yerde topladık.
    private static void AddCommonYarpServices(IServiceCollection services, IOptions<List<YARPOptions>?> yarpOptions)
    {
        services.AddSingleton<ITokenProvider, MultiJwtTokenProvider>();
        services.AddHttpClient();

        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        yarpOptions.Value?.ForEach(option =>
        {

            var tokenProvider = services.BuildServiceProvider().GetRequiredService<ITokenProvider>();

            // In-memory route konfigürasyonu
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
                        { "PathRemovePrefix", option.PrefixPath } // örn: "app"
                    },
                    new Dictionary<string, string>
                    {
                        //maybe not needed
                        { "PathPrefix", "/api" }
                    }
                }
            };
            routes.Add(route);


            option.Clusters.ForEach(cluster =>
            {
                // In-memory cluster konfigürasyonu
                var clusterConfig = new ClusterConfig
                {
                    ClusterId = option.ClusterId,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        { cluster.Name, new DestinationConfig { Address = cluster.Address } }
                    }
                };
                clusters.Add(clusterConfig);
            });
        });

        services.AddSingleton<ITokenProvider, MultiJwtTokenProvider>();

        services.AddReverseProxy()
            .LoadFromMemory(routes, clusters)
            .AddTransforms(builderContext =>
            {
                // Ek olarak, transform pipeline üzerinden header'lar üzerinde daha fazla kontrol sağlayabilirsiniz.
                builderContext.AddRequestTransform(async transformContext =>
                {
                    // DI'dan IAuthService örneğini alıyoruz.
                    var authService = transformContext.HttpContext.RequestServices.GetRequiredService<IAuthService>();

                    // GetUserProfileAsync ile kullanıcı profilini alıyoruz
                    var userProfile = await authService.GetUserProfileAsync(transformContext.HttpContext);
                    var userId = userProfile?.UserId.ToString() ?? string.Empty;

                    // Saklanacak header'ları belirleyelim.
                    var headersToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "X-Private-Key"
                    };

                    // Silinmesi gereken header isimlerini listeleyelim.
                    var headersToRemove = transformContext.ProxyRequest.Headers
                        .Where(header => !headersToKeep.Contains(header.Key))
                        .Select(header => header.Key)
                        .ToList();

                    // Seçilen header'ları sil.
                    foreach (var headerKey in headersToRemove)
                    {
                        transformContext.ProxyRequest.Headers.Remove(headerKey);
                    }

                    var baseUrl = transformContext.DestinationPrefix;
                    if (string.IsNullOrEmpty(baseUrl)) return;

                    var option = yarpOptions.Value?.FirstOrDefault(x =>
                        x.Clusters.Any(c => c.Address.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase)));

                    if (!string.IsNullOrEmpty(option.PrivateKey))
                    {
                        var tokenProvider = transformContext.HttpContext.RequestServices.GetRequiredService<ITokenProvider>();

                        var cluster = option.Clusters.FirstOrDefault(x =>
                            x.Address.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase));

                        if (option is not null)
                        {
                            var token = await tokenProvider.GetTokenAsync(baseUrl, cluster.Name, option.PrivateKey);

                            transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        }
                    }

                    //if (!string.IsNullOrEmpty(userId))
                    //{
                    //    transformContext.ProxyRequest.Headers.Add("X-User-Id", userId);
                    //}

                    //// Cookie adınızı "YourJwtCookieName" ile değiştirin.
                    //var jwt = transformContext.HttpContext.Request.Cookies["YourJwtCookieName"];
                    //if (!string.IsNullOrEmpty(jwt))
                    //{
                    //    var handler = new JwtSecurityTokenHandler();
                    //    var token = handler.ReadJwtToken(jwt);
                    //    // Token içerisindeki "id" ya da "sub" claim'ini çekebilirsiniz.
                    //    var userIdClaim = token.Claims.FirstOrDefault(c => c.Type == "id" || c.Type == "sub");
                    //    if (userIdClaim != null)
                    //    {
                    //        transformContext.ProxyRequest.Headers.Add("X-User-Id", userIdClaim.Value);
                    //    }
                    //}


                    //var authService = transformContext.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                    //var userId = await authService.GetUserIdAsync(transformContext.HttpContext);

                    if (!string.IsNullOrEmpty(userId))
                    {
                        transformContext.ProxyRequest.Headers.Add("X-User-Id", userId);
                    }


                });
            });
    }

    public static IApplicationBuilder UseProxy(this IApplicationBuilder app)
    {
        // Reverse Proxy middleware'ini uygulamaya ekleyin.
        app.UseEndpoints(endpoints =>
        {
            // Burada endpoints üzerinden MapReverseProxy çağrısı yapıyoruz.
            endpoints.MapReverseProxy();
        });

        return app;
    }
}


