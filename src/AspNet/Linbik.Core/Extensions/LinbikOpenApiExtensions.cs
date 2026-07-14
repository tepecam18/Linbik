using Linbik.Core.OpenApi;
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

        // Vertical-slice konvansiyonunda her slice kendi iç içe `Request`/`Response`
        // tipini tanımlar (ör. CreateComment.Request, DeleteComment.Request). .NET'in
        // varsayılan şema kimliği yalnız Type.Name kullanır — bu da TÜM slice'lardaki
        // "Request"/"Response" tiplerinin aynı OpenAPI şema kimliğine çakışmasına yol
        // açar (gateway aggregator'da ilk gelen kazanır, diğerleri sessizce kaybolur).
        // İç içe Request/Response için üst (slice) tipin adını öne ekleyerek benzersizleştir.
        var defaultSchemaId = options.CreateSchemaReferenceId;
        options.CreateSchemaReferenceId = typeInfo =>
        {
            var type = typeInfo.Type;
            if (type.DeclaringType is not null && (type.Name == "Request" || type.Name == "Response"))
            {
                return type.DeclaringType.Name + type.Name;
            }
            return defaultSchemaId(typeInfo);
        };

        return options;
    }
}
