using Linbik.Core;
using Linbik.Core.Responses;
using Linbik.Server.Interfaces;
using Linbik.Server.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Linbik.Server;

public static class ServerExtensions
{
    public static ILinbikBuilder AddLinbikServer(this ILinbikBuilder builder, Action<ServerOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);
        var optionsInstance = new ServerOptions();
        configureOptions(optionsInstance);
        var serverOptions = Options.Create(optionsInstance);

        AddCommonServerServices(builder.Services, serverOptions);

        return builder;
    }

    public static ILinbikBuilder AddLinbikServer(this ILinbikBuilder builder)
    {
        var serviceProvider = builder.Services.BuildServiceProvider();
        var configuration = serviceProvider.GetService<IConfiguration>();
        builder.Services.Configure<ServerOptions>(configuration.GetSection("Linbik:Server"));
        var serverOptions = Options.Create(configuration.GetSection("Linbik:Server").Get<ServerOptions>());

        AddCommonServerServices(builder.Services, serverOptions);

        return builder;
    }

    public static void AddCommonServerServices(IServiceCollection services, IOptions<ServerOptions> serverOptions)
    {
        //services.AddSingleton(serverOptions);
    }

    public static IApplicationBuilder UseLinbikServer(this IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/linbik/app-login", async (HttpContext context, [FromServices] ILinbikServerRepository serverRepository, [FromBody] AppLoginRequest request) =>
            {


                if (await serverRepository.AppLoginValidations(request.appId, request.key))
                {

                }

                LBaseResponse<AppLoginResponse> response = new();
                return Results.Ok(response);

            }).WithTags("Linbik").WithName("Linbik App Login");
        });

        return app;
    }
}
