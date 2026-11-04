using Linbik.Core.OpenApi;
using Linbik.Core.Responses;
using Microsoft.AspNetCore.OpenApi;

namespace Linbik.Core.Extensions;

/// <summary>
/// OpenAPI <see cref="OpenApiOptions"/> üzerinde Linbik akış uzantılarını
/// (<c>linbik-flows</c>) kayıt eden helper'lar.
/// </summary>
public static class LinbikOpenApiExtensions
{
    /// <summary>
    /// <see cref="LFlowOpenApiOperationTransformer"/>'i operation transformer
    /// olarak kaydeder. <c>AddOpenApi(opt =&gt; opt.AddLinbikFlowExtension())</c>
    /// şeklinde kullanın.
    /// </summary>
    public static OpenApiOptions AddLinbikFlowExtension(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddOperationTransformer<LFlowOpenApiOperationTransformer>();

        var schemaId = options.CreateSchemaReferenceId;
        options.CreateSchemaReferenceId = typeInfo =>
        {
            var type = typeInfo.Type;
            var id = schemaId(typeInfo);
            if (id is null)
                return null;
            if (IsSliceContract(type))
                return SliceContractName(type);
            if (type.IsConstructedGenericType &&
                type.GetGenericTypeDefinition() == typeof(LBaseResponse<>) &&
                IsSliceContract(type.GenericTypeArguments[0]))
                return "LBaseResponseOf" + SliceContractName(type.GenericTypeArguments[0]);
            return id;
        };

        return options;
    }

    private static bool IsSliceContract(Type type) =>
        type.IsNested && type.Name is "Request" or "Response";

    private static string SliceContractName(Type type) =>
        type.DeclaringType is { } owner
            ? SliceContractName(owner) + type.Name.Split('`')[0]
            : type.Name.Split('`')[0];
}
