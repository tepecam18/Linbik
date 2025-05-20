using Linbik.Core;
using Linbik.Core.Interfaces;
using Linbik.JwtAuthManager.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Linbik.JwtAuthManager;

public static class JwtAuthManagerExtensions
{
    public static ILinbikBuilder AddJwtAuth(this ILinbikBuilder builder, Action<JwtAuthOptions> configureOptions, bool useInMemory = false)
    {
        builder.Services.Configure(configureOptions);

        if (useInMemory)
        {
            builder.Services.AddSingleton<ILinbikRepository, InMemoryLinbikRepository>();
        }

        var optionsInstance = new JwtAuthOptions();
        configureOptions(optionsInstance);

        // IOptions haline getiriyoruz.
        var jwtOptions = Options.Create(optionsInstance);

        AddCommonAuthServices(builder.Services, jwtOptions);

        return builder;
    }

    public static ILinbikBuilder AddJwtAuth(this ILinbikBuilder builder, bool useInMemory = false)
    {
        var serviceProvider = builder.Services.BuildServiceProvider();
        var configuration = serviceProvider.GetService<IConfiguration>();

        builder.Services.Configure<JwtAuthOptions>(configuration.GetSection("Linbik:JwtAuth"));

        if (useInMemory)
        {
            builder.Services.AddSingleton<ILinbikRepository, InMemoryLinbikRepository>();
        }

        var jwtOptions = Options.Create(configuration.GetSection("Linbik:JwtAuth").Get<JwtAuthOptions>());

        AddCommonAuthServices(builder.Services, jwtOptions);

        return builder;
    }

    private static void AddCommonAuthServices(IServiceCollection services, IOptions<JwtAuthOptions> jwtOptions)
    {
        services.AddSingleton<IAuthService, JwtAuthService>();
    }
    public static IApplicationBuilder UseJwtAuth(this IApplicationBuilder app)
    {
        //TODO: bagimsiz DI lari kaldir
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/linbik/login", async (HttpContext context, [FromServices] IOptions<JwtAuthOptions> options, [FromServices] ITokenValidator validator, [FromServices] ILinbikRepository repository) =>
            {
                try
                {
                    var token = context.Request.Form["token"].FirstOrDefault();
                    if (string.IsNullOrEmpty(token))
                        return Results.BadRequest("Invalid Token");

                    var result = await validator.ValidateToken(token)
                        .ConfigureAwait(false);

                    if (result is null)
                        return Results.BadRequest("Invalid Token");

                    if (!result.Success)
                        return Results.BadRequest(result.Message);

                    string refreshToken = "";
                    var claims = new List<Claim>();

                    await repository.CreateRefresToken(result.UserGuid, result.Name, out refreshToken, out claims)
                    .ConfigureAwait(false);

                    claims.Add(new Claim(ClaimTypes.Name, result.Name));
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, result.UserGuid.ToString()));

                    var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.Value.privateKey));

                    var cred = new SigningCredentials(key, options.Value.algorithm);

                    var securityToken = new JwtSecurityToken(
                        claims: claims,
                        expires: DateTime.Now.AddMinutes(options.Value.accessTokenExpiration),
                        signingCredentials: cred);

                    var jwt = new JwtSecurityTokenHandler();

                    var jwtToken = jwt.WriteToken(securityToken);

                    context.Response.Cookies.Append("authToken", jwtToken, cookieOptions);
                    context.Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
                    context.Response.Cookies.Append("userName", result.Name);

                    return Results.Ok("Login Success");
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            }).WithTags("Linbik");

            endpoints.MapPost("/linbik/refresh-login", async (HttpContext context, [FromServices] IOptions<JwtAuthOptions> options, [FromServices] ILinbikRepository repository) =>
            {
                string currentRefreshToken = context.Request.Cookies["refreshToken"];

                if (currentRefreshToken is null)
                    return Results.BadRequest("Invalid Token");

                var result = await repository.UseRefresToken(currentRefreshToken)
                   .ConfigureAwait(false);

                if (result is null)
                    return Results.BadRequest("Invalid Token");

                if (!result.Success)
                    return Results.BadRequest(result.Message);

                await repository.LoggedInUser(result.UserGuid, result.Name)
                    .ConfigureAwait(false);

                string refreshToken = "";
                var claims = new List<Claim>();

                await repository.CreateRefresToken(result.UserGuid, result.Name, out refreshToken, out claims)
                .ConfigureAwait(false);

                claims.Add(new Claim(ClaimTypes.Name, result.Name));
                claims.Add(new Claim(ClaimTypes.NameIdentifier, result.UserGuid.ToString()));

                var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.Value.privateKey));

                var cred = new SigningCredentials(key, options.Value.algorithm);

                var securityToken = new JwtSecurityToken(
                    claims: claims,
                    expires: DateTime.Now.AddMinutes(options.Value.accessTokenExpiration),
                    signingCredentials: cred);

                var jwt = new JwtSecurityTokenHandler();

                var jwtToken = jwt.WriteToken(securityToken);

                context.Response.Cookies.Append("authToken", jwtToken, cookieOptions);
                context.Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
                context.Response.Cookies.Append("userName", result.Name);

                return Results.Ok("Refresh Token");
            }).WithTags("Linbik");
        });

        return app;
    }

    static CookieOptions cookieOptions = new CookieOptions
    {
        HttpOnly = true,
        Secure = true, // HTTP olduğu için secure bayrağı false
        SameSite = SameSiteMode.None,
        Expires = DateTime.UtcNow.AddDays(15)
    };
}

