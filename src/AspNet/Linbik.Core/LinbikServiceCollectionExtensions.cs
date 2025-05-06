using Linbik.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik;

public static class LinbikServiceCollectionExtensions
{

    public static ILinbikBuilder AddLinbik(this IServiceCollection services, Action<LinbikOptions> configureOptions)
    {
        services.Configure(configureOptions);
        AddCommonAuthServices(services);
        return new LinbikBuilder(services);
    }

    public static ILinbikBuilder AddLinbik(this IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider.GetService<IConfiguration>();

        services.Configure<LinbikOptions>(configuration.GetSection("Linbik"));
        AddCommonAuthServices(services);
        return new LinbikBuilder(services);
    }

    // Ortak servis kayıtlarını tek bir yerde topladık.
    private static void AddCommonAuthServices(IServiceCollection services)
    {
        services.AddSingleton<ITokenValidator, TokenValidator>();
    }
}
