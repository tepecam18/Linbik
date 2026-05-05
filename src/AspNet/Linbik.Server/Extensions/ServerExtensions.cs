using Linbik.Core;
using Linbik.Core.Builders.Interfaces;
using Linbik.Core.Services.Interfaces;
using Linbik.Server.Configuration;
using Linbik.Server.Interfaces;
using Linbik.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Linbik.Server.Extensions;

/// <summary>
/// Extension methods for configuring Linbik Server (integration service side).
/// Provides JWT validation with RSA public keys for both user-service and S2S authentication.
/// </summary>
public static class ServerExtensions
{
    /// <summary>
    /// Add Linbik Server services with custom options (builder pattern).
    /// Registers JWT authentication schemes and token validator.
    /// </summary>
    public static ILinbikBuilder AddLinbikServer(this ILinbikBuilder builder, Action<ServerOptions> configureOptions)
    {
        var options = new ServerOptions();
        configureOptions(options);
        builder.Services.Configure(configureOptions);
        AddCommonServerServices(builder.Services, options);
        return builder;
    }

    /// <summary>
    /// Add Linbik Server services from configuration (builder pattern).
    /// Registers JWT authentication schemes and token validator.
    /// </summary>
    /// <param name="builder">The Linbik builder.</param>
    /// <param name="configuration">The application configuration.</param>
    public static ILinbikBuilder AddLinbikServer(this ILinbikBuilder builder, IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = configuration.Get<ServerOptions>() ?? new ServerOptions();
        builder.Services.Configure<ServerOptions>(configuration);
        AddCommonServerServices(builder.Services, options);
        return builder;
    }

    public static ILinbikBuilder AddLinbikServer(this ILinbikBuilder builder)
    {
        builder.AddLinbikServer(_ => { });
        return builder;
    }

    private static void AddCommonServerServices(IServiceCollection services, ServerOptions options)
    {
        // Add integration token validator
        services.AddSingleton<IntegrationTokenValidator>();
        services.AddSingleton<IValidateOptions<ServerOptions>, ServerOptionsValidator>();
        services.AddSingleton<ILinbikStartupValidator, ServerStartupValidator>();

        // Optional DI: kullanıcı kendi ILinbikIntegrationHandler'ını kayıt etmediyse,
        // default LinbikIntegrationHandler (yalnızca log yazar, başarı döner) devreye girer.
        // Override için: services.AddLinbikIntegrationHandler<MyHandler>();
        // TryAdd kullanıldığı için kullanıcının daha önce kaydettiği handler ezilmez.
        services.TryAddScoped<ILinbikIntegrationHandler, LinbikIntegrationHandler>();

        // Default no-op security event sink. Tüketici uygulama kendi adapter'ını (ör. ServiceEventLog'a
        // yazan) kayıt edebilir; TryAdd sayesinde önceden kayıtlı sink ezilmez.
        services.TryAddSingleton<ILinbikSecurityEventSink, NoOpSecurityEventSink>();

        // Add JWT authentication schemes if public key is configured
        if (!string.IsNullOrEmpty(options.PublicKey))
        {
            AddLinbikAuthentication(services, options);
        }
    }

    /// <summary>
    /// Add Linbik authentication schemes for both user-service and S2S scenarios.
    /// Both schemes validate JWT with the same RSA public key but expect different claims.
    /// </summary>
    private static void AddLinbikAuthentication(IServiceCollection services, ServerOptions options)
    {
        // Create RSA key as a managed singleton to ensure proper lifecycle
        var rsaKey = CreateRsaSecurityKey(options.PublicKey);

        services.AddAuthentication(authOptions =>
        {
            // Don't set default scheme - let controllers choose with attributes:
            // [LinbikDelegatedAuthorize] for user-initiated requests
            // [LinbikApplicationAuthorize] for service-to-service requests
        })
        // User-Service scheme: expects user claims (sub, name, preferred_username, azp)
        // Rejects S2S tokens (token_type == "s2s") to prevent cross-scheme injection
        .AddJwtBearer(LinbikDefaults.DelegatedScheme, jwtOptions =>
        {
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = rsaKey,
                ValidateLifetime = true,
                ValidateIssuer = options.ValidateIssuer,
                ValidIssuer = options.JwtIssuer,
                ValidateAudience = options.ValidateAudience,
                ValidAudience = options.PackageName,
                ClockSkew = TimeSpan.FromMinutes(options.ClockSkewMinutes)
            };
            jwtOptions.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var tokenType = context.Principal?.FindFirst("token_type")?.Value;
                    if (tokenType == "s2s")
                    {
                        context.Fail("Application tokens are not accepted by LinbikDelegated scheme. Use [LinbikDelegatedAuthorize] instead.");
                    }
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = ctx => ReportAuthFailureAsync(ctx, isS2S: false),
                OnChallenge = ctx => ReportChallengeAsync(ctx, isS2S: false),
                OnForbidden = ctx => ReportForbiddenAsync(ctx, isS2S: false),
            };
        })
        // S2S scheme: expects service claims only (source_service_id, source_package_name, role)
        // Rejects user-service tokens (missing token_type == "s2s") to prevent cross-scheme injection
        .AddJwtBearer(LinbikDefaults.ApplicationScheme, jwtOptions =>
        {
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = rsaKey,
                ValidateLifetime = true,
                ValidateIssuer = options.ValidateIssuer,
                ValidIssuer = options.JwtIssuer,
                ValidateAudience = options.ValidateAudience,
                ValidAudience = options.PackageName,
                ClockSkew = TimeSpan.FromMinutes(options.ClockSkewMinutes)
            };
            jwtOptions.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var tokenType = context.Principal?.FindFirst("token_type")?.Value;
                    if (tokenType != "s2s")
                    {
                        context.Fail("Only application tokens (token_type=s2s) are accepted by LinbikApplication scheme. Use [LinbikDelegatedAuthorize] for user tokens.");
                    }
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = ctx => ReportAuthFailureAsync(ctx, isS2S: true),
                OnChallenge = ctx => ReportChallengeAsync(ctx, isS2S: true),
                OnForbidden = ctx => ReportForbiddenAsync(ctx, isS2S: true),
            };
        });

        services.AddAuthorization();
    }

    /// <summary>
    /// JWT validation hatası anında security event sink'i çağırır. Sink hata fırlatırsa
    /// authentication akışı bozulmasın diye tüm exception'lar swallow edilir.
    /// </summary>
    private static async Task ReportAuthFailureAsync(
        Microsoft.AspNetCore.Authentication.JwtBearer.AuthenticationFailedContext ctx,
        bool isS2S)
    {
        try
        {
            var sink = ctx.HttpContext.RequestServices.GetService<ILinbikSecurityEventSink>();
            if (sink is null or NoOpSecurityEventSink) return;

            await sink.ReportAsync(BuildEvent(
                ctx.HttpContext,
                eventType: isS2S ? LinbikSecurityEventType.S2sJwtInvalid : LinbikSecurityEventType.AuthenticationFailed,
                message: ctx.Exception?.Message ?? "JWT authentication failed.",
                statusCode: 401,
                metadata: new
                {
                    scheme = ctx.Scheme.Name,
                    exception_type = ctx.Exception?.GetType().Name,
                }), ctx.HttpContext.RequestAborted);
        }
        catch
        {
            // Sink hatasını swallow et: auth akışını bozmamalı.
        }
    }

    /// <summary>
    /// 401 challenge yazıldığı anda tetiklenir. Authentication header eksik / token okunamadı
    /// gibi durumlar için OnAuthenticationFailed dışında kalan path'i de yakalar.
    /// </summary>
    private static async Task ReportChallengeAsync(
        Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerChallengeContext ctx,
        bool isS2S)
    {
        // OnAuthenticationFailed zaten çalıştıysa ikinci kez raporlama (AuthenticateFailure dolu olur).
        if (ctx.AuthenticateFailure is not null) return;

        try
        {
            var sink = ctx.HttpContext.RequestServices.GetService<ILinbikSecurityEventSink>();
            if (sink is null or NoOpSecurityEventSink) return;

            await sink.ReportAsync(BuildEvent(
                ctx.HttpContext,
                eventType: isS2S ? LinbikSecurityEventType.S2sJwtInvalid : LinbikSecurityEventType.AuthenticationFailed,
                message: ctx.ErrorDescription ?? ctx.Error ?? "Authentication challenge issued (401).",
                statusCode: 401,
                metadata: new { scheme = ctx.Scheme.Name, error = ctx.Error }),
                ctx.HttpContext.RequestAborted);
        }
        catch
        {
            // Swallow.
        }
    }

    /// <summary>
    /// Token geçerli ama policy/role yetki vermediğinde tetiklenir (HTTP 403).
    /// </summary>
    private static async Task ReportForbiddenAsync(
        Microsoft.AspNetCore.Authentication.JwtBearer.ForbiddenContext ctx,
        bool isS2S)
    {
        try
        {
            var sink = ctx.HttpContext.RequestServices.GetService<ILinbikSecurityEventSink>();
            if (sink is null or NoOpSecurityEventSink) return;

            var actor = ctx.HttpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? ctx.HttpContext.User?.FindFirst("sub")?.Value;

            Guid? sourceServiceId = null;
            if (isS2S)
            {
                var sourceClaim = ctx.HttpContext.User?.FindFirst("source_service_id")?.Value;
                if (Guid.TryParse(sourceClaim, out var parsed)) sourceServiceId = parsed;
            }

            await sink.ReportAsync(new LinbikSecurityEvent
            {
                EventType = LinbikSecurityEventType.AuthorizationFailed,
                Message = "Authenticated principal lacks required role/policy (403).",
                HttpStatusCode = 403,
                RequestPath = ctx.HttpContext.Request.Path.Value,
                RequestMethod = ctx.HttpContext.Request.Method,
                RemoteIp = ctx.HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = ctx.HttpContext.Request.Headers.UserAgent.ToString(),
                ActorUserId = actor,
                SourceServiceId = sourceServiceId,
                Metadata = new
                {
                    scheme = ctx.Scheme.Name,
                    is_s2s = isS2S,
                    roles = ctx.HttpContext.User?.FindAll(System.Security.Claims.ClaimTypes.Role)
                        .Select(r => r.Value).ToArray(),
                },
            }, ctx.HttpContext.RequestAborted);
        }
        catch
        {
            // Swallow.
        }
    }

    private static LinbikSecurityEvent BuildEvent(
        Microsoft.AspNetCore.Http.HttpContext httpContext,
        string eventType,
        string message,
        int statusCode,
        object? metadata)
    {
        return new LinbikSecurityEvent
        {
            EventType = eventType,
            Message = message,
            HttpStatusCode = statusCode,
            RequestPath = httpContext.Request.Path.Value,
            RequestMethod = httpContext.Request.Method,
            RemoteIp = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            Metadata = metadata,
        };
    }

    /// <summary>
    /// Creates an RsaSecurityKey from a Base64-encoded public key.
    /// The RSA instance is NOT disposed because it must remain valid for the application lifetime.
    /// </summary>
    private static RsaSecurityKey CreateRsaSecurityKey(string publicKeyBase64)
    {
        try
        {
            var rsa = RSA.Create();
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            return new RsaSecurityKey(rsa);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new ArgumentException(
                "Invalid RSA public key format. Ensure the key is Base64-encoded in SubjectPublicKeyInfo format.",
                nameof(publicKeyBase64),
                ex);
        }
    }

    /// <summary>
    /// Startup validator for Linbik.Server module.
    /// Forces eager validation of <see cref="ServerOptions"/> and verifies critical service registrations.
    /// </summary>
    private sealed class ServerStartupValidator : ILinbikStartupValidator
    {
        public string ModuleName => "Linbik.Server";
        public int Order => 20;

        public void Validate(IServiceProvider services)
        {
            // Force eager validation of ServerOptions (triggers ServerOptionsValidator)
            var options = services.GetRequiredService<IOptions<ServerOptions>>();
            _ = options.Value;

            // Verify IntegrationTokenValidator is registered
            _ = services.GetService<IntegrationTokenValidator>()
                ?? throw new InvalidOperationException(
                    "IntegrationTokenValidator is not registered. Call services.AddLinbikServer() or builder.AddLinbikServer() in Program.cs.");
        }
    }
}
