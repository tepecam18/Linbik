using System.Text.Json.Nodes;
using Linbik.Core.Attributes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Linbik.Core.OpenApi;

/// <summary>
/// Her OpenAPI operation'ına <c>linbik-flows</c> uzantısını ekler. Değer,
/// action üzerindeki <see cref="LFlowAuthorizeAttribute"/> kuralından türetilir:
/// <list type="bullet">
/// <item><description><c>["*"]</c> — yetkilendirme şartı yok (anonim).</description></item>
/// <item><description><c>["authenticated"]</c> — herhangi bir akış kabul (parametresiz attribute).</description></item>
/// <item><description><c>["Self", "Delegated", ...]</c> — yalnız listelenen akışlar.</description></item>
/// </list>
/// API Gateway ve diğer client'lar bu uzantıyı okuyarak dokümantasyonu flow'a göre
/// filtreleyebilir veya preflight enforcement uygulayabilir.
/// </summary>
public sealed class LFlowOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>Operation extension anahtarı.</summary>
    public const string ExtensionKey = "linbik-flows";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var attr = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<LFlowAuthorizeAttribute>()
            .LastOrDefault();

        var jsonArray = new JsonArray();

        if (attr is null)
        {
            jsonArray.Add("*");
        }
        else if (attr.AllowedFlows.Length == 0)
        {
            jsonArray.Add("authenticated");
        }
        else
        {
            foreach (var flow in attr.AllowedFlows)
                jsonArray.Add(flow);
        }

        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[ExtensionKey] = new JsonNodeExtension(jsonArray);

        return Task.CompletedTask;
    }
}
