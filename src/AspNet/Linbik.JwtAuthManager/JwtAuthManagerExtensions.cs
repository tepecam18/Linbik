using Linbik.Core;
using Linbik.Core.Interfaces;
using Linbik.Core.Responses;
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
        var options = app.ApplicationServices.GetRequiredService<IOptions<JwtAuthOptions>>().Value;

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // HTTP olduğu için secure bayrağı false
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(options.refreshTokenExpiration)
        };

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(options.loginPath, async (HttpContext context, [FromServices] ITokenValidator validator, [FromServices] ILinbikRepository repository) =>
            {
                try
                {
                    if (options.refererControl)
                    {
                        var referer = context.Request.Headers["Referer"].ToString();

                        if (referer != "https://linbik.com/")
                            return Results.BadRequest("invalid_referer");
                    }

                    #region Token Validetions

                    var token = context.Request.Query["token"].FirstOrDefault();

                    if (string.IsNullOrEmpty(token))
                        return Results.BadRequest("Invalid Token");

                    var verifier = PkceService.GetVerifier(context.Request) ?? "";
                    var validate = await validator.ValidateToken(token, verifier)
                        .ConfigureAwait(false);

                    if (validate is null)
                        return Results.BadRequest("Invalid Token");

                    if (!validate.Success)
                    {
                        return Results.BadRequest(validate.Message);
                        //return Results.Redirect("https://localhost:7020?error=" + result.Message);
                    }


                    var userGuidStr = validate.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
                    var userGuid = Guid.Parse(userGuidStr);
                    var name = validate.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
                    #endregion

                    string refreshToken = "";
                    var claims = new List<Claim>();

                    await repository.CreateRefresToken(userGuid, name, out refreshToken)
                    .ConfigureAwait(false);

                    claims.Add(new Claim(ClaimTypes.Name, name));
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, userGuidStr));

                    var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.privateKey));

                    var cred = new SigningCredentials(key, options.algorithm);

                    var securityToken = new JwtSecurityToken(
                        claims: claims,
                        expires: DateTime.Now.AddMinutes(options.accessTokenExpiration),
                        signingCredentials: cred);

                    var jwt = new JwtSecurityTokenHandler();

                    var jwtToken = jwt.WriteToken(securityToken);

                    context.Response.Cookies.Append("authToken", jwtToken, cookieOptions);
                    context.Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
                    context.Response.Cookies.Append("userName", name, new()
                    {
                        Secure = true,
                        Expires = DateTime.Now.AddMinutes(options.accessTokenExpiration - 1),
                        SameSite = SameSiteMode.None
                    });

                    var route = options.routes?[context.Request.Query["route"].FirstOrDefault() ?? ""];
                    var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "";

                    if (string.IsNullOrEmpty(route))
                        route = options.routes?.FirstOrDefault().Value ?? "";

                    if (returnUrl.ToLower().Contains(route.ToLower()))
                        return Results.Redirect(returnUrl);

                    if (string.IsNullOrEmpty(route))
                        return Results.Ok(new LBaseResponse<object>
                        {
                            isSuccess = true,
                            data = null
                        });

                    return Results.Redirect(route);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            }).WithTags("Linbik");

            endpoints.MapPost(options.refreshLoginPath, async (HttpContext context, [FromServices] ILinbikRepository repository) =>
            {
                string currentRefreshToken = context.Request.Cookies["refreshToken"];

                if (currentRefreshToken is null)
                {
                    var response2 = new LBaseResponse<object>(
                        title: "refresh_token_error",
                        message: "Refresh Token is null, please login again.",
                        _isSuccess: false
                        );

                    return Results.BadRequest(response2);
                }

                var result = await repository.UseRefresToken(currentRefreshToken)
                   .ConfigureAwait(false);

                if (!result.Success)
                {
                    var response2 = new LBaseResponse<object>(
                        title: "refresh_token_error",
                        message: result.Message ?? "Refresh Token is invalid, please login again.",
                        _isSuccess: false
                        );
                    return Results.BadRequest(response2);
                }

                await repository.LoggedInUser(result.UserGuid, result.Name)
                    .ConfigureAwait(false);

                string refreshToken = "";
                var claims = new List<Claim>();

                await repository.CreateRefresToken(result.UserGuid, result.Name, out refreshToken)
                .ConfigureAwait(false);

                claims.Add(new Claim(ClaimTypes.Name, result.Name));
                claims.Add(new Claim(ClaimTypes.NameIdentifier, result.UserGuid.ToString()));

                var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.privateKey));

                var cred = new SigningCredentials(key, options.algorithm);

                var securityToken = new JwtSecurityToken(
                    claims: claims,
                    expires: DateTime.Now.AddMinutes(options.accessTokenExpiration),
                    signingCredentials: cred);

                var jwt = new JwtSecurityTokenHandler();

                var jwtToken = jwt.WriteToken(securityToken);

                context.Response.Cookies.Append("authToken", jwtToken, cookieOptions);
                context.Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
                context.Response.Cookies.Append("userName", result.Name, new()
                {
                    Secure = true,
                    Expires = DateTime.Now.AddMinutes(options.accessTokenExpiration - 1),
                    SameSite = SameSiteMode.None
                });

                var response = new LBaseResponse<object>()
                {
                    isSuccess = true,
                    data = null
                };

                return Results.Ok(response);
            }).WithTags("Linbik");

            endpoints.MapPost(options.exitPath, async (HttpContext context) =>
            {
                context.Response.Cookies.Delete("authToken", cookieOptions);
                context.Response.Cookies.Delete("refreshToken", cookieOptions);
                context.Response.Cookies.Delete("userName", cookieOptions);

                var response = new LBaseResponse<object>()
                {
                    isSuccess = true,
                    data = null
                };

                return Results.Ok(response);
            }).WithTags("Linbik");

            endpoints.MapPost(options.pkceStartPath, (HttpContext ctx) =>
            {
                var authorize = PkceService.BuildAuthorizeBody(ctx.Response);

                var response = new LBaseResponse<object>(authorize);

                return Results.Ok(response);
            }).WithTags("Linbik").AllowAnonymous();
        });

        return app;
    }
}

