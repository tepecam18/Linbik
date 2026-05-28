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
        return options;
    }
}
