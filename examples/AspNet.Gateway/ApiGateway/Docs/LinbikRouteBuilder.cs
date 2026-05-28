using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Docs;

/// <summary>
/// <see cref="LinbikGatewaySource"/> listesinden YARP <see cref="RouteConfig"/> ve
/// <see cref="ClusterConfig"/> öğelerini üretir. Konvansiyon:
/// <list type="bullet">
/// <item><c>self-{prefix}</c> → <c>/{prefix}/{**catch-all}</c>, policy <c>LinbikSelfOptional</c>, flow <c>Self</c></item>
/// <item><c>delegated-{prefix}</c> → <c>/delegated/{prefix}/{**catch-all}</c>, policy <c>LinbikDelegatedOptional</c>, flow <c>Delegated</c></item>
/// <item><c>apps-{prefix}</c> → <c>/apps/{prefix}/{**catch-all}</c>, policy <c>LinbikApplicationOptional</c>, flow <c>Application</c></item>
/// </list>
/// <para>
/// <b>Service-authoritative</b>: gateway route'ları opsiyonel policy'ler kullanır —
/// auth scheme'i tetiklenir (claim çıkarımı), ama anonim istek reddedilmez. Asıl
/// güvenlik kapısı downstream'deki <c>[LFlowAuthorize]</c> attribute'udur:
/// <c>Linbik-Flow</c> header'ı transform tarafından <b>yalnız authenticated</b>
/// istekler için yazılır, dolayısıyla anonim istek attribute taşıyan op'a 401 alır,
/// <c>linbik-flows: ["*"]</c> taşıyan anonim op'a düzgün geçer.
/// </para>
/// <para>
/// <see cref="LinbikGatewaySource.IsGateway"/> <c>true</c> olan kaynak için route üretilmez —
/// gateway'in kendi controller'ları (örn. <c>/api/linbik/login</c>) için yalnız doc aggregation
/// yapılır.
/// </para>
/// Tüm route'lar downstream'e <c>/api/{prefix}/{**catch-all}</c> path pattern'ı ile yönlenir.
/// </summary>
public static class LinbikRouteBuilder
{
    public const string FlowMetadataKey = "Linbik-Flow";

    public static (IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters) Build(
        IEnumerable<LinbikGatewaySource> sources)
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        foreach (var s in sources)
        {
            if (string.IsNullOrWhiteSpace(s.ServicePrefix) || string.IsNullOrWhiteSpace(s.Address))
                continue;

            // Gateway'in kendisi: route üretme, yalnız doc aggregation hedefi.
            if (s.IsGateway)
                continue;

            var clusterId = $"{s.ServicePrefix}-cluster";
            clusters.Add(new ClusterConfig
            {
                ClusterId = clusterId,
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    [s.ServicePrefix] = new DestinationConfig { Address = s.Address }
                }
            });

            var downstreamPath = $"/api/{s.ServicePrefix}/{{**catch-all}}";
            IReadOnlyList<IReadOnlyDictionary<string, string>> transforms = new[]
            {
                (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["PathPattern"] = downstreamPath }
            };

            routes.Add(MakeRoute($"self-{s.ServicePrefix}", clusterId,
                $"/{s.ServicePrefix}/{{**catch-all}}", "LinbikSelfOptional", "Self", transforms));

            routes.Add(MakeRoute($"delegated-{s.ServicePrefix}", clusterId,
                $"/delegated/{s.ServicePrefix}/{{**catch-all}}", "LinbikDelegatedOptional", "Delegated", transforms));

            routes.Add(MakeRoute($"apps-{s.ServicePrefix}", clusterId,
                $"/apps/{s.ServicePrefix}/{{**catch-all}}", "LinbikApplicationOptional", "Application", transforms));
        }

        return (routes, clusters);
    }

    private static RouteConfig MakeRoute(
        string routeId,
        string clusterId,
        string matchPath,
        string authPolicy,
        string flow,
        IReadOnlyList<IReadOnlyDictionary<string, string>> transforms)
    {
        return new RouteConfig
        {
            RouteId = routeId,
            ClusterId = clusterId,
            AuthorizationPolicy = authPolicy,
            Match = new RouteMatch { Path = matchPath },
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [FlowMetadataKey] = flow
            },
            Transforms = transforms
        };
    }
}
