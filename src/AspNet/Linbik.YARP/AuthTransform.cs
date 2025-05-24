using Linbik.JwtAuthManager;
using Linbik.YARP.Interfaces;
using System.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

namespace Linbik.YARP;

public class AuthTransform(ITokenProvider tokenProvider, List<ClusterOptions> clusterOptions, string privateKey) : RequestTransform
{
    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        var uri = context.ProxyRequest.RequestUri;
        if (uri == null) return;

        var baseUrl = $"{uri.Scheme}://{uri.Host}".TrimEnd('/');

        var option = clusterOptions.FirstOrDefault(x =>
            x.address.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase));

        if (option is not null)
        {
            var token = await tokenProvider.GetTokenAsync(baseUrl, option.name, privateKey);
            context.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}