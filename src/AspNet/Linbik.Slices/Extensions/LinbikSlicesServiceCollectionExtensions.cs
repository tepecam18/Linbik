using Linbik.Slices.Endpoints;
using Linbik.Slices.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Linbik.Slices;

/// <summary>
/// Slice altyapısının çekirdek DI kayıtları. Source generator'ın ürettiği
/// <c>AddLinbikSlices</c>, önce bu metodu çağırır, sonra slice handler/validator
/// kayıtlarını ekler.
/// </summary>
public static class LinbikSlicesServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="ILinbikSender"/>'ı (scoped) kaydeder. Slice handler ve validator
    /// kayıtları source generator tarafından ayrıca eklenir.
    /// </summary>
    public static IServiceCollection AddLinbikSender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ILinbikSender, LinbikSender>();
        return services;
    }

    /// <summary>
    /// JSON gövde bağlama hatalarını (<see cref="LinbikJsonExceptionHandler"/>) standart
    /// <c>LBaseResponse</c> sözleşmesine çeviren global exception handler'ı kaydeder.
    /// <c>app.UseExceptionHandler()</c> ile birlikte kullanılmalı (Program.cs'de erken).
    /// </summary>
    public static IServiceCollection AddLinbikSliceExceptionHandling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddExceptionHandler<LinbikJsonExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }

    /// <summary>
    /// <see cref="AddLinbikSliceExceptionHandling"/> ile kaydedilen handler'ı pipeline'a
    /// bağlar. Routing'ten ÖNCE, olabildiğince erken çağrılmalı.
    /// </summary>
    public static IApplicationBuilder UseLinbikSliceExceptionHandling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseExceptionHandler();
        return app;
    }
}

