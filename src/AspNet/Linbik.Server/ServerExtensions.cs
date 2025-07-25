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
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
        var config = serviceProvider.GetService<IConfiguration>();
        builder.Services.Configure<ServerOptions>(config.GetSection("Linbik:Server"));
        var serverOptions = Options.Create(config.GetSection("Linbik:Server").Get<ServerOptions>());

        AddCommonServerServices(builder.Services, serverOptions);

        return builder;
    }

    public static void AddCommonServerServices(IServiceCollection services, IOptions<ServerOptions> serverOptions)
    {
        //services.AddSingleton(serverOptions);
    }

    public static IApplicationBuilder UseLinbikServer(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<ServerOptions>>().Value;

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost(options.loginPath, async (HttpContext context, [FromServices] ILinbikServerRepository serverRepository, [FromBody] AppLoginRequest request) =>
            {

                var validator = await serverRepository.AppLoginValidationsAsync(request);


                if (!validator.success)
                {
                    LBaseResponse<AppLoginResponse> badResponse = new("AppLoginFailed", "App login failed");
                    return Results.BadRequest(badResponse);
                }


                validator.claims.Add(new Claim(ClaimTypes.Name, request.appId.ToString()));


                var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.privateKey));

                var cred = new SigningCredentials(key, options.algorithm);

                var securityToken = new JwtSecurityToken(
                    claims: validator.claims,
                    expires: DateTime.Now.AddMinutes(options.accessTokenExpiration),
                    signingCredentials: cred);

                var jwt = new JwtSecurityTokenHandler();

                var jwtToken = jwt.WriteToken(securityToken);


                LBaseResponse<AppLoginResponse> response = new(new AppLoginResponse()
                {
                    token = jwtToken,
                    expiresIn = options.accessTokenExpiration
                });

                return Results.Ok(response);

            }).WithTags("Linbik").WithName("Linbik App Login");
        });

        return app;
    }
}
