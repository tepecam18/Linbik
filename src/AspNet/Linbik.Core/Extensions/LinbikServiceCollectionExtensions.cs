using Linbik.Core.Builders;
using Linbik.Core.Configuration;
using Linbik.Core.Interfaces;
using Linbik.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Linbik.Core.Extensions;

public static class LinbikServiceCollectionExtensions
{
    /// <summary>
    /// Default HttpClient name for Linbik authentication client
    /// </summary>
    public const string LinbikHttpClientName = "LinbikAuthClient";

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

        // Configure resilience options
        services.AddOptions<ResilienceOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection("Linbik:Resilience").Bind(options);
            });

        // Configure audit options
        services.AddOptions<AuditOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection("Linbik:Audit").Bind(options);
            });

        // Configure rate limit options
        services.AddOptions<RateLimitOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection("Linbik:RateLimit").Bind(options);
            });

        // Register audit logger
        services.AddSingleton<IAuditLogger, DefaultAuditLogger>();

        // Register metrics
        services.AddSingleton<LinbikMetrics>();

        // Add typed HttpClient for LinbikAuthClient with resilience
        services.AddHttpClient<ILinbikAuthClient, LinbikAuthClient>(LinbikHttpClientName)
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<LinbikOptions>>().Value;
                if (!string.IsNullOrEmpty(options.LinbikUrl))
                {
                    client.BaseAddress = new Uri(options.LinbikUrl.TrimEnd('/') + "/");
                }
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddResilienceHandler("LinbikResilience", (builder, context) =>
            {
                var resilienceOptions = context.ServiceProvider
                    .GetService<IOptions<ResilienceOptions>>()?.Value ?? new ResilienceOptions();

                if (!resilienceOptions.Enabled)
                    return;

                // Add retry policy with exponential backoff
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = resilienceOptions.MaxRetryAttempts,
                    Delay = TimeSpan.FromMilliseconds(resilienceOptions.RetryDelayMs),
                    MaxDelay = TimeSpan.FromMilliseconds(resilienceOptions.MaxRetryDelayMs),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = args => ValueTask.FromResult(
                        args.Outcome.Exception != null ||
                        (args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError))
                });

                // Add circuit breaker
                if (resilienceOptions.CircuitBreakerEnabled)
                {
                    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                    {
                        FailureRatio = 0.5,
                        MinimumThroughput = resilienceOptions.CircuitBreakerFailureThreshold,
                        SamplingDuration = TimeSpan.FromSeconds(resilienceOptions.CircuitBreakerSamplingDurationSeconds),
                        BreakDuration = TimeSpan.FromSeconds(resilienceOptions.CircuitBreakerDurationSeconds)
                    });
                }

                // Add timeout
                builder.AddTimeout(TimeSpan.FromSeconds(resilienceOptions.TimeoutSeconds));
            });

        // Register main auth service
        services.AddScoped<IAuthService, LinbikAuthService>();
    }
}
