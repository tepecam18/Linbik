using Linbik.Slices.Pipeline;
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
}
