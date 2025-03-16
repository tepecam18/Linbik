using Linbik.Interfaces;
using Linbik.JwtAuthManager;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Linbik.YARP;

public static class YARPManagerExtensions
{
    public static ILinbikBuilder AddProxy(this ILinbikBuilder builder, Action<List<YARPOptions>> configureOptions)
    {
        builder.Services.Configure(configureOptions);

        var optionsInstance = new List<YARPOptions>();
        configureOptions(optionsInstance);

        // IOptions haline getiriyoruz.
        var yarpOptions = Options.Create(optionsInstance);

        AddCommonAuthServices(builder.Services, yarpOptions);

        return builder;
    }

    public static ILinbikBuilder AddProxy(this ILinbikBuilder builder, IConfiguration configuration)
    {
        builder.Services.Configure<List<YARPOptions>>(configuration.GetSection("Linbik:Yarp"));

        var yarpOptions = Options.Create(configuration.GetSection("Linbik:Yarp").Get<List<YARPOptions>>());

        AddCommonAuthServices(builder.Services, yarpOptions);

        return builder;
    }

    // Ortak servis kayıtlarını tek bir yerde topladık.
    private static void AddCommonAuthServices(IServiceCollection services, IOptions<List<YARPOptions>> yarpOptions)
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        yarpOptions.Value.ForEach(option =>
        {
            // In-memory route konfigürasyonu
            var route = new RouteConfig
            {
                RouteId = option.RouteId,
                ClusterId = option.ClusterId,
                Match = new RouteMatch { Path = option.prefixPath + "/{**catch-all}" },
                Transforms = new List<Dictionary<string, string>>
                {
                    // Private key'i içeren yeni bir header ekliyoruz.
                    new Dictionary<string, string>
                    {
                        { "RequestHeader", "X-Private-Key" }, // Eklemek istediğiniz header adını belirleyin.
                        { "Set", option.privateKey } // Buraya private key değeriniz gelecek.
                    }
                }
            };
            routes.Add(route);


            option.clusters.ForEach(cluster =>
            {
                // In-memory cluster konfigürasyonu
                var clusterConfig = new ClusterConfig
                {
                    ClusterId = option.ClusterId,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        { cluster.name, new DestinationConfig { Address = cluster.address } }
                    }
                };
                clusters.Add(clusterConfig);
            });
        });


        services.AddReverseProxy()
            .LoadFromMemory(routes, clusters)
            .AddTransforms(builderContext =>
            {
                // Ek olarak, transform pipeline üzerinden header'lar üzerinde daha fazla kontrol sağlayabilirsiniz.
                builderContext.AddRequestTransform(async transformContext =>
                {
                    //TODO: userId koy buraya
                        transformContext.ProxyRequest.Headers.Add("X-User-Id", "user1");
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


