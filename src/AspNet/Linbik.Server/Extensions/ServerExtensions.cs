using Linbik.Core.Interfaces;
using Linbik.Core.Responses;
using Linbik.Server.Configuration;
using Linbik.Server.Interfaces;
using Linbik.Server.Models;
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

namespace Linbik.Server.Extensions;

public static class ServerExtensions
{
    private const string LinbikTag = "Linbik";
    private const string AppLoginEndpointName = "Linbik App Login";
    private const string AppIdClaimType = "appId";
    private const string UserTypeClaimType = "userType";

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
        var configuration = builder.Services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        builder.Services.Configure<ServerOptions>(configuration.GetSection("Linbik:Server"));
        var serverOptions = Options.Create(configuration.GetSection("Linbik:Server").Get<ServerOptions>());

        AddCommonServerServices(builder.Services, serverOptions);

        return builder;
    }

    private static void AddCommonServerServices(IServiceCollection services, IOptions<ServerOptions> serverOptions)
    {
        // TODO: Add server-specific services here
    }

    public static IApplicationBuilder UseLinbikServer(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<ServerOptions>>().Value;

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost(options.LoginPath, async (HttpContext context, [FromServices] ILinbikServerRepository serverRepository, [FromBody] AppLoginModel request) =>
            {
                if (request == null)
                {
                    var badResponse = new LBaseResponse<AppLoginResponse>("AppLoginFailed", "Request is null");
                    return Results.BadRequest(badResponse);
                }

                var validator = await serverRepository.AppLoginValidationsAsync(request);

                if (!validator.Success)
                {
                    var badResponse = new LBaseResponse<AppLoginResponse>("AppLoginFailed", validator.Message ?? "App login failed");
                    return Results.BadRequest(badResponse);
                }

                // ✅ DOĞRU - App için gerekli claim'ler
                validator.Claims.Add(new Claim(AppIdClaimType, request.AppId.ToString()));
                validator.Claims.Add(new Claim(UserTypeClaimType, "App"));

                var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.PrivateKey));
                var cred = new SigningCredentials(key, options.Algorithm);

                var securityToken = new JwtSecurityToken(
                    claims: validator.Claims,
                    expires: DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration),
                    signingCredentials: cred);

                var jwt = new JwtSecurityTokenHandler();
                var jwtToken = jwt.WriteToken(securityToken);

                var response = new LBaseResponse<AppLoginResponse>(new AppLoginResponse()
                {
                    Token = jwtToken,
                    ExpiresIn = options.AccessTokenExpiration
                });

                return Results.Ok(response);

            }).WithTags(LinbikTag).WithName(AppLoginEndpointName);
        });

        return app;
    }
}
