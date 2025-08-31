using Linbik.Core.Interfaces;
using Linbik.Core.Responses;
using Linbik.Core.Services;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Interfaces;
using Linbik.JwtAuthManager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Linbik.JwtAuthManager.Extensions;

public static class JwtAuthManagerExtensions
{
    private const string IdClaimType = "userId";
    private const string NameClaimType = "firstName";
    private const string AuthTokenCookie = "authToken";
    private const string RefreshTokenCookie = "refreshToken";
    private const string UserTypeClaimType = "userType";
    private const string UserNameCookie = "userName";
    private const string DefaultRoute = "default";

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

        var service = app.ApplicationServices.GetService<ILinbikRepository>();

        if (service == null)
        {
            throw new InvalidOperationException(
                "Please register ILinbikRepository in the DI container, " +
                "or use AddJwtAuth(true) in Program.cs."
            );
        }

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(options.RefreshTokenExpiration)
        };

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(options.LoginPath, async (HttpContext context, [FromServices] ITokenValidator validator, [FromServices] ILinbikRepository repository) =>
            {
                try
                {
                    if (options.RefererControl)
                    {
                        var referer = context.Request.Headers["Referer"].ToString();

                        if (referer != "https://linbik.com/")
                            return Results.BadRequest(new LBaseResponse<object>("Invalid Referer"));
                    }

                    #region Token Validations

                    var token = context.Request.Query["token"].FirstOrDefault();

                    if (string.IsNullOrEmpty(token))
                        return Results.Json(new LBaseResponse<object>("Invalid Token"), statusCode: StatusCodes.Status406NotAcceptable);

                    var verifier = PkceService.GetVerifier(context.Request) ?? "";
                    var validate = await validator.ValidateToken(token, verifier, options.PkceEnabled)
                        .ConfigureAwait(false);

                    if (validate is null)
                        return Results.Json(new LBaseResponse<object>("Invalid Token"), statusCode: StatusCodes.Status406NotAcceptable);

                    if (!validate.Success)
                    {
                        return Results.Json(new LBaseResponse<object>(validate.Message ?? "Something went wrong with validation"), statusCode: StatusCodes.Status406NotAcceptable);
                    }

                    var userGuidStr = validate.Claims?.FirstOrDefault(c => c.Type == IdClaimType)?.Value;
                    if (string.IsNullOrEmpty(userGuidStr) || !Guid.TryParse(userGuidStr, out var userGuid))
                        return Results.Json(new LBaseResponse<object>("Invalid user ID"), statusCode: StatusCodes.Status406NotAcceptable);

                    var firstName = validate.Claims?.FirstOrDefault(c => c.Type == NameClaimType)?.Value ?? "Unknown";
                    #endregion

                    var (refreshToken, success) = await repository.CreateRefresToken(userGuid, firstName)
                        .ConfigureAwait(false);

                    if (!success)
                        return Results.BadRequest(new LBaseResponse<object>("Failed to create refresh token"));

                    var claims = new List<Claim>
                    {
                        new Claim(NameClaimType, firstName),
                        new Claim(IdClaimType, userGuidStr),
                        new Claim(UserTypeClaimType, "User")
                    };

                    var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.PrivateKey));
                    var cred = new SigningCredentials(key, options.Algorithm);

                    var securityToken = new JwtSecurityToken(
                        claims: claims,
                        expires: DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration),
                        signingCredentials: cred);

                    var jwt = new JwtSecurityTokenHandler();
                    var jwtToken = jwt.WriteToken(securityToken);

                    context.Response.Cookies.Append(AuthTokenCookie, jwtToken, cookieOptions);
                    context.Response.Cookies.Append(RefreshTokenCookie, refreshToken, cookieOptions);
                    context.Response.Cookies.Append(UserNameCookie, firstName, new()
                    {
                        Secure = true,
                        Expires = DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration - 1),
                        SameSite = SameSiteMode.None
                    });

                    var routeKey = context.Request.Query["route"].FirstOrDefault() ?? DefaultRoute;
                    options.Routes.TryGetValue(routeKey, out var route);
                    var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "";

                    if (string.IsNullOrEmpty(route))
                        route = options.Routes?.FirstOrDefault().Value ?? "";

                    if (string.IsNullOrEmpty(route))
                        return Results.Ok(new LBaseResponse<object>
                        {
                            IsSuccess = true,
                            Data = null
                        });

                    if (returnUrl.ToLower().Contains(route.ToLower()))
                        return Results.Redirect(returnUrl);

                    return Results.Redirect(route);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new LBaseResponse<object>(ex.Message));
                }
            }).WithTags("Linbik");

            endpoints.MapPost(options.RefreshLoginPath, async (HttpContext context, [FromServices] ILinbikRepository repository) =>
            {
                string? currentRefreshToken = context.Request.Cookies[RefreshTokenCookie];

                if (string.IsNullOrEmpty(currentRefreshToken))
                {
                    var response2 = new LBaseResponse<object>(
                        title: "refresh_token_error",
                        message: "Refresh Token is null, please login again.",
                        isSuccess: false
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
                        isSuccess: false
                        );
                    return Results.BadRequest(response2);
                }

                await repository.LoggedInUser(result.UserGuid, result.Name)
                    .ConfigureAwait(false);

                var (refreshToken, success) = await repository.CreateRefresToken(result.UserGuid, result.Name)
                    .ConfigureAwait(false);

                if (!success)
                    return Results.BadRequest(new LBaseResponse<object>("Failed to create refresh token"));

                var claims = new List<Claim>
                {
                    new Claim(NameClaimType, result.Name ?? "Unknown"),
                    new Claim(IdClaimType, result.UserGuid.ToString()),
                    new Claim(UserTypeClaimType, "User")
                };

                var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.PrivateKey));
                var cred = new SigningCredentials(key, options.Algorithm);

                var securityToken = new JwtSecurityToken(
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration),
                    signingCredentials: cred);

                var jwt = new JwtSecurityTokenHandler();
                var jwtToken = jwt.WriteToken(securityToken);

                context.Response.Cookies.Append(AuthTokenCookie, jwtToken, cookieOptions);
                context.Response.Cookies.Append(RefreshTokenCookie, refreshToken, cookieOptions);
                context.Response.Cookies.Append(UserNameCookie, result.Name ?? "Unknown", new()
                {
                    Secure = true,
                    Expires = DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration - 1),
                    SameSite = SameSiteMode.None
                });

                var response = new LBaseResponse<object>()
                {
                    IsSuccess = true,
                    Data = null
                };

                return Results.Ok(response);
            }).WithTags("Linbik");

            endpoints.MapPost(options.ExitPath, async (HttpContext context) =>
            {
                context.Response.Cookies.Delete(AuthTokenCookie, cookieOptions);
                context.Response.Cookies.Delete(RefreshTokenCookie, cookieOptions);
                context.Response.Cookies.Delete(UserNameCookie, cookieOptions);

                var response = new LBaseResponse<object>()
                {
                    IsSuccess = true,
                    Data = null
                };

                return Results.Ok(response);
            }).WithTags("Linbik");

            if (options.PkceEnabled)
            {
                endpoints.MapPost(options.PkceStartPath, (HttpContext ctx) =>
                {
                    var authorize = PkceService.BuildAuthorizeBody(ctx.Response);

                    var response = new LBaseResponse<object>(authorize);

                    return Results.Ok(response);
                }).WithTags("Linbik").AllowAnonymous();
            }
        });

        return app;
    }
}

