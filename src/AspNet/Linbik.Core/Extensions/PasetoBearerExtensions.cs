using Linbik.Core.Services;
using Microsoft.AspNetCore.Authentication;

namespace Linbik.Core.Extensions;

/// <summary>
/// PASETO v4.public Bearer şemasını kayıt etmek için yardımcı extension'lar.
/// <c>Linbik.Server</c>, <c>Linbik.Api</c> veya herhangi bir host bu helper'ı kullanarak
/// kendi şemasını ekleyebilir.
/// </summary>
public static class PasetoBearerExtensions
{
    /// <summary>
    /// Belirtilen şema adı için <see cref="PasetoBearerHandler"/>'ı kaydeder.
    /// </summary>
    /// <param name="builder">Authentication builder.</param>
    /// <param name="schemeName">Şema adı (örn. <c>LinbikDefaults.DelegatedScheme</c>).</param>
    /// <param name="configureOptions">Şemaya özgü <see cref="PasetoBearerOptions"/> ayarlaması.</param>
    public static AuthenticationBuilder AddLinbikPasetoBearer(
        this AuthenticationBuilder builder,
        string schemeName,
        Action<PasetoBearerOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(schemeName);
        ArgumentNullException.ThrowIfNull(configureOptions);

        return builder.AddScheme<PasetoBearerOptions, PasetoBearerHandler>(schemeName, configureOptions);
    }
}
