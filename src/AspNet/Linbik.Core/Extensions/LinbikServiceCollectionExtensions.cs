using Linbik.Core.Builders;
using Linbik.Core.Configuration;
using Linbik.Core.Interfaces;
using Linbik.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Core.Extensions;

public static class LinbikServiceCollectionExtensions
{
    public static LinbikBuilder AddLinbik(this IServiceCollection services, Action<LinbikOptions> configureOptions)
    {
        services.Configure(configureOptions);
        AddCommonAuthServices(services);
        return new LinbikBuilder(services);
    }

    public static LinbikBuilder AddLinbik(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LinbikOptions>(configuration.GetSection("Linbik"));
        AddCommonAuthServices(services);
        return new LinbikBuilder(services);
    }

    public static LinbikBuilder AddLinbik(this IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider.GetService<IConfiguration>();

        services.Configure<LinbikOptions>(configuration?.GetSection("Linbik") ?? throw new InvalidOperationException("Linbik configuration section not found"));
        AddCommonAuthServices(services);
        return new LinbikBuilder(services);
    }

    // Ortak servis kayıtlarını tek bir yerde topladık.
    private static void AddCommonAuthServices(IServiceCollection services)
    {
        // Add HttpContextAccessor for session access
        services.AddHttpContextAccessor();

        // Add session services
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(24); // 24 hour session
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        // Add HttpClient for LinbikClient
        services.AddHttpClient<IAuthService, LinbikClient>();

        // Legacy token validator
        services.AddSingleton<ITokenValidator, TokenValidator>();
    }
}
