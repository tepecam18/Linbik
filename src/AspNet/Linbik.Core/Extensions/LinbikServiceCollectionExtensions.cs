using Linbik.Core.Builders;
using Linbik.Core.Builders.Interfaces;
using Linbik.Core.Configuration;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Linbik.Core.Extensions;

/// <summary>
/// Extension methods for adding Linbik authentication services to the DI container.
/// </summary>
public static class LinbikServiceCollectionExtensions
{
    /// <summary>
    /// Default HttpClient name for Linbik authentication client (handles both user-context and S2S operations)
    /// </summary>
    public const string LinbikHttpClientName = "LinbikAuthClient";

    /// <summary>
    /// Adds Linbik authentication services with custom options configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure <see cref="LinbikOptions"/>.</param>
    /// <returns>A <see cref="LinbikBuilder"/> for further configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when configureOptions is null.</exception>
    public static LinbikBuilder AddLinbik(this IServiceCollection services, Action<LinbikOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);
        services.Configure(configureOptions);
        services.AddCommonAuthServices();
        return new LinbikBuilder(services);
    }


    /// <summary>
    /// Adds Linbik authentication services using configuration from IConfiguration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>A <see cref="LinbikBuilder"/> for further configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    public static LinbikBuilder AddLinbik(this IServiceCollection services, IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<LinbikOptions>(configuration);
        services.AddCommonAuthServices();
        return new LinbikBuilder(services);
    }

    /// <summary>
    /// Adds common authentication services shared by all overloads.
    /// </summary>
    private static IServiceCollection AddCommonAuthServices(this IServiceCollection services)
    {

        services.AddSingleton<IValidateOptions<LinbikOptions>, LinbikOptionsValidator>();
        services.AddSingleton<ILinbikStartupValidator, CoreStartupValidator>();

        // Add HttpContextAccessor for cookie-based authentication
        services.AddHttpContextAccessor();

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

        // Register Keyless Mode services (credential store + provision client)
        services.AddSingleton<ILinbikCredentialStore, FileLinbikCredentialStore>();
        services.AddSingleton<LinbikProvisionClient>();

        // Register heartbeat background service
        services.AddHostedService<LinbikHeartbeatService>();

        return services;
    }

    /// <summary>
    /// Startup validator for Linbik.Core module.
    /// Forces eager validation of <see cref="LinbikOptions"/> and verifies critical service registrations.
    /// </summary>
    private sealed class CoreStartupValidator : ILinbikStartupValidator
    {
        public string ModuleName => "Linbik.Core";
        public int Order => 0;

        public void Validate(IServiceProvider services)
        {
            // Force eager validation of LinbikOptions (triggers LinbikOptionsValidator)
            var options = services.GetRequiredService<IOptions<LinbikOptions>>();
            _ = options.Value;

            // Verify critical services are registered
            _ = services.GetService<ILinbikAuthClient>()
                ?? throw new InvalidOperationException(
                    "ILinbikAuthClient is not registered. Call services.AddLinbik() or builder.AddLinbik() in Program.cs.");
        }
    }
}
