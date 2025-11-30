using Linbik.Core.Interfaces;
using Linbik.Core.Models;
using Linbik.Core.Responses;
using Linbik.Core.Services;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Interfaces;
using Linbik.JwtAuthManager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private const string LinbikRefreshTokenCookie = "linbikRefreshToken";
    private const string HasIntegrationsCookie = "hasIntegrations";
    private const string UserTypeClaimType = "userType";
    private const string UserNameCookie = "userName";
    private const string DefaultRoute = "default";
    private const string IntegrationTokenPrefix = "integration_";

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

        using (var scope = app.ApplicationServices.CreateScope())
        {
            var service = app.ApplicationServices.GetService<ILinbikRepository>();

            if (service == null)
            {
                throw new InvalidOperationException(
                    "Please register ILinbikRepository in the DI container, " +
                    "or use AddJwtAuth(true) in Program.cs."
                );
            }
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
            // Login endpoint - exchange authorization code for tokens
            endpoints.MapGet(options.LoginPath, async (HttpContext context, 
                [FromServices] ILinbikAuthClient linbikClient, 
                [FromServices] ILinbikRepository repository,
                [FromServices] ILogger<ILinbikAuthClient> logger) =>
            {
                try
                {
                    if (options.RefererControl)
                    {
                        var referer = context.Request.Headers["Referer"].ToString();
                        if (referer != "https://linbik.com/")
                            return Results.BadRequest(new LBaseResponse<object>("Invalid Referer"));
                    }

                    #region Code Exchange with LinbikAuthClient

                    var code = context.Request.Query["code"].FirstOrDefault();

                    if (string.IsNullOrEmpty(code))
                        return Results.Json(new LBaseResponse<object>("Invalid Code"), statusCode: StatusCodes.Status406NotAcceptable);

                    // Exchange code for tokens using LinbikAuthClient
                    var tokenResponse = await linbikClient.ExchangeCodeAsync(code).ConfigureAwait(false);

                    if (tokenResponse is null)
                        return Results.Json(new LBaseResponse<object>("Token exchange failed"), statusCode: StatusCodes.Status406NotAcceptable);

                    // PKCE verification (client-side)
                    if (options.PkceEnabled)
                    {
                        if (string.IsNullOrEmpty(tokenResponse.CodeChallenge))
                        {
                            logger.LogWarning("PKCE is enabled but CodeChallenge is missing in token response");
                            return Results.Json(new LBaseResponse<object>("PKCE verification failed"), statusCode: StatusCodes.Status406NotAcceptable);
                        }
                        
                        var verifier = PkceService.GetVerifier(context.Request);
                        if (!string.IsNullOrEmpty(verifier))
                        {
                            if (!PkceService.VerifyChallengeMatches(verifier, tokenResponse.CodeChallenge))
                            {
                                logger.LogWarning("PKCE verification failed for user {UserId}", tokenResponse.UserId);
                                return Results.Json(new LBaseResponse<object>("PKCE verification failed"), statusCode: StatusCodes.Status406NotAcceptable);
                            }
                            PkceService.DeleteVerifier(context.Response);
                        }
                    }

                    var userGuid = tokenResponse.UserId;
                    var userName = tokenResponse.Username;
                    var displayName = tokenResponse.DisplayName;

                    #endregion

                    // Calculate token expiration from response or use default
                    var accessTokenExpiry = tokenResponse.AccessTokenExpiresAt.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.AccessTokenExpiresAt.Value).UtcDateTime
                        : DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration);

                    var hasIntegrations = tokenResponse.Integrations?.Count > 0;
                    string localRefreshToken;

                    if (hasIntegrations)
                    {
                        // Integrations exist - use Linbik's refresh token
                        localRefreshToken = tokenResponse.RefreshToken ?? string.Empty;
                        
                        // Store Linbik refresh token in separate cookie
                        context.Response.Cookies.Append(LinbikRefreshTokenCookie, localRefreshToken, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.None,
                            Expires = tokenResponse.RefreshTokenExpiresAt.HasValue
                                ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.RefreshTokenExpiresAt.Value).UtcDateTime
                                : DateTime.UtcNow.AddDays(options.RefreshTokenExpiration)
                        });

                        // Store integration tokens in cookies
                        foreach (var integration in tokenResponse.Integrations!)
                        {
                            var integrationCookieName = $"{IntegrationTokenPrefix}{integration.PackageName}";
                            context.Response.Cookies.Append(integrationCookieName, integration.AccessToken, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.None,
                                Expires = accessTokenExpiry
                            });
                        }
                    }
                    else
                    {
                        // No integrations - create local refresh token
                        var (refreshToken, success) = await repository.CreateRefresToken(userGuid, userName)
                            .ConfigureAwait(false);

                        if (!success)
                            return Results.BadRequest(new LBaseResponse<object>("Failed to create refresh token"));

                        localRefreshToken = refreshToken;
                    }

                    // Mark whether we have integrations (affects refresh behavior)
                    context.Response.Cookies.Append(HasIntegrationsCookie, hasIntegrations.ToString().ToLower(), new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = DateTime.UtcNow.AddDays(options.RefreshTokenExpiration)
                    });

                    // Create local JWT token
                    var claims = new List<Claim>
                    {
                        new Claim(NameClaimType, displayName),
                        new Claim(IdClaimType, userGuid.ToString()),
                        new Claim(UserTypeClaimType, "User")
                    };

                    var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.PrivateKey));
                    var cred = new SigningCredentials(key, options.Algorithm);

                    var securityToken = new JwtSecurityToken(
                        claims: claims,
                        expires: accessTokenExpiry,
                        signingCredentials: cred);

                    var jwt = new JwtSecurityTokenHandler();
                    var jwtToken = jwt.WriteToken(securityToken);

                    context.Response.Cookies.Append(AuthTokenCookie, jwtToken, cookieOptions);
                    context.Response.Cookies.Append(RefreshTokenCookie, localRefreshToken, cookieOptions);
                    context.Response.Cookies.Append(UserNameCookie, displayName, new CookieOptions
                    {
                        Secure = true,
                        Expires = accessTokenExpiry.AddMinutes(-1),
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

            // Refresh endpoint - handles both local and Linbik refresh tokens
            endpoints.MapPost(options.RefreshLoginPath, async (HttpContext context, 
                [FromServices] ILinbikRepository repository,
                [FromServices] ILinbikAuthClient linbikClient,
                [FromServices] ILogger<ILinbikAuthClient> logger) =>
            {
                var hasIntegrationsStr = context.Request.Cookies[HasIntegrationsCookie];
                var hasIntegrations = hasIntegrationsStr?.ToLower() == "true";

                if (hasIntegrations)
                {
                    // === SCENARIO 1: Has Integrations - Use Linbik Refresh ===
                    var linbikRefreshToken = context.Request.Cookies[LinbikRefreshTokenCookie];

                    if (string.IsNullOrEmpty(linbikRefreshToken))
                    {
                        return Results.BadRequest(new LBaseResponse<object>(
                            title: "refresh_token_error",
                            message: "Linbik Refresh Token is null, please login again.",
                            isSuccess: false
                        ));
                    }

                    // Call Linbik to refresh tokens
                    var tokenResponse = await linbikClient.RefreshTokensAsync(linbikRefreshToken).ConfigureAwait(false);

                    if (tokenResponse is null)
                    {
                        return Results.BadRequest(new LBaseResponse<object>(
                            title: "refresh_token_error",
                            message: "Failed to refresh tokens from Linbik, please login again.",
                            isSuccess: false
                        ));
                    }

                    var userGuid = tokenResponse.UserId;
                    var displayName = tokenResponse.DisplayName;

                    // Calculate token expiration
                    var accessTokenExpiry = tokenResponse.AccessTokenExpiresAt.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.AccessTokenExpiresAt.Value).UtcDateTime
                        : DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration);

                    // Update Linbik refresh token cookie
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                    {
                        context.Response.Cookies.Append(LinbikRefreshTokenCookie, tokenResponse.RefreshToken, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.None,
                            Expires = tokenResponse.RefreshTokenExpiresAt.HasValue
                                ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.RefreshTokenExpiresAt.Value).UtcDateTime
                                : DateTime.UtcNow.AddDays(options.RefreshTokenExpiration)
                        });
                    }

                    // Update integration token cookies
                    if (tokenResponse.Integrations?.Count > 0)
                    {
                        foreach (var integration in tokenResponse.Integrations)
                        {
                            var integrationCookieName = $"{IntegrationTokenPrefix}{integration.PackageName}";
                            context.Response.Cookies.Append(integrationCookieName, integration.AccessToken, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.None,
                                Expires = accessTokenExpiry
                            });
                        }
                    }

                    // Create new local JWT token
                    var claims = new List<Claim>
                    {
                        new Claim(NameClaimType, displayName),
                        new Claim(IdClaimType, userGuid.ToString()),
                        new Claim(UserTypeClaimType, "User")
                    };

                    var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.PrivateKey));
                    var cred = new SigningCredentials(key, options.Algorithm);

                    var securityToken = new JwtSecurityToken(
                        claims: claims,
                        expires: accessTokenExpiry,
                        signingCredentials: cred);

                    var jwt = new JwtSecurityTokenHandler();
                    var jwtToken = jwt.WriteToken(securityToken);

                    context.Response.Cookies.Append(AuthTokenCookie, jwtToken, cookieOptions);
                    context.Response.Cookies.Append(RefreshTokenCookie, tokenResponse.RefreshToken ?? string.Empty, cookieOptions);
                    context.Response.Cookies.Append(UserNameCookie, displayName, new CookieOptions
                    {
                        Secure = true,
                        Expires = accessTokenExpiry.AddMinutes(-1),
                        SameSite = SameSiteMode.None
                    });

                    return Results.Ok(new LBaseResponse<object>
                    {
                        IsSuccess = true,
                        Data = null
                    });
                }
                else
                {
                    // === SCENARIO 2: No Integrations - Use Local Refresh ===
                    string? currentRefreshToken = context.Request.Cookies[RefreshTokenCookie];

                    if (string.IsNullOrEmpty(currentRefreshToken))
                    {
                        return Results.BadRequest(new LBaseResponse<object>(
                            title: "refresh_token_error",
                            message: "Refresh Token is null, please login again.",
                            isSuccess: false
                        ));
                    }

                    var result = await repository.UseRefresToken(currentRefreshToken).ConfigureAwait(false);

                    if (!result.Success)
                    {
                        return Results.BadRequest(new LBaseResponse<object>(
                            title: "refresh_token_error",
                            message: result.Message ?? "Refresh Token is invalid, please login again.",
                            isSuccess: false
                        ));
                    }

                    await repository.LoggedInUser(result.UserGuid, result.Name).ConfigureAwait(false);

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
                    context.Response.Cookies.Append(UserNameCookie, result.Name ?? "Unknown", new CookieOptions
                    {
                        Secure = true,
                        Expires = DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration - 1),
                        SameSite = SameSiteMode.None
                    });

                    return Results.Ok(new LBaseResponse<object>
                    {
                        IsSuccess = true,
                        Data = null
                    });
                }
            }).WithTags("Linbik");

            // Logout endpoint - clear all cookies
            endpoints.MapPost(options.ExitPath, async (HttpContext context) =>
            {
                context.Response.Cookies.Delete(AuthTokenCookie, cookieOptions);
                context.Response.Cookies.Delete(RefreshTokenCookie, cookieOptions);
                context.Response.Cookies.Delete(LinbikRefreshTokenCookie, cookieOptions);
                context.Response.Cookies.Delete(HasIntegrationsCookie, cookieOptions);
                context.Response.Cookies.Delete(UserNameCookie, cookieOptions);

                // Delete all integration token cookies
                foreach (var cookie in context.Request.Cookies)
                {
                    if (cookie.Key.StartsWith(IntegrationTokenPrefix))
                    {
                        context.Response.Cookies.Delete(cookie.Key, cookieOptions);
                    }
                }

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

    /// <summary>
    /// Get integration token from cookie by package name
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="packageName">Integration service package name</param>
    /// <returns>JWT token or null if not found</returns>
    public static string? GetIntegrationToken(HttpContext context, string packageName)
    {
        var cookieName = $"{IntegrationTokenPrefix}{packageName}";
        return context.Request.Cookies.TryGetValue(cookieName, out var token) ? token : null;
    }

    /// <summary>
    /// Get all integration tokens from cookies
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Dictionary of package name to JWT token</returns>
    public static Dictionary<string, string> GetAllIntegrationTokens(HttpContext context)
    {
        var tokens = new Dictionary<string, string>();
        
        foreach (var cookie in context.Request.Cookies)
        {
            if (cookie.Key.StartsWith(IntegrationTokenPrefix))
            {
                var packageName = cookie.Key.Substring(IntegrationTokenPrefix.Length);
                tokens[packageName] = cookie.Value;
            }
        }
        
        return tokens;
    }

    /// <summary>
    /// Check if user has integrations (requires Linbik refresh)
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>True if user has integration services</returns>
    public static bool HasIntegrations(HttpContext context)
    {
        var value = context.Request.Cookies[HasIntegrationsCookie];
        return value?.ToLower() == "true";
    }

    /// <summary>
    /// Map Linbik OAuth endpoints (login, refresh, logout)
    /// This is the preferred way to add Linbik endpoints in .NET 6+ minimal APIs
    /// </summary>
    /// <param name="endpoints">Endpoint route builder</param>
    /// <returns>Endpoint route builder for chaining</returns>
    public static IEndpointRouteBuilder MapLinbikEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<JwtAuthOptions>>().Value;

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(options.RefreshTokenExpiration)
        };

        // Login endpoint - exchange authorization code for tokens
        endpoints.MapGet(options.LoginPath, async (HttpContext context,
            [FromServices] ILinbikAuthClient linbikClient,
            [FromServices] ILinbikRepository repository,
            [FromServices] ILogger<ILinbikAuthClient> logger) =>
        {
            try
            {
                var code = context.Request.Query["code"].FirstOrDefault();

                if (string.IsNullOrEmpty(code))
                    return Results.Json(new LBaseResponse<object>("Invalid Code"), statusCode: StatusCodes.Status406NotAcceptable);

                // Exchange code for tokens using LinbikAuthClient
                var tokenResponse = await linbikClient.ExchangeCodeAsync(code).ConfigureAwait(false);

                if (tokenResponse is null)
                    return Results.Json(new LBaseResponse<object>("Token exchange failed"), statusCode: StatusCodes.Status406NotAcceptable);

                // PKCE verification (client-side)
                if (options.PkceEnabled)
                {
                    if (string.IsNullOrEmpty(tokenResponse.CodeChallenge))
                    {
                        logger.LogWarning("PKCE is enabled but CodeChallenge is missing in token response");
                        return Results.Json(new LBaseResponse<object>("PKCE verification failed"), statusCode: StatusCodes.Status406NotAcceptable);
                    }

                    var verifier = PkceService.GetVerifier(context.Request);
                    if (!string.IsNullOrEmpty(verifier))
                    {
                        if (!PkceService.VerifyChallengeMatches(verifier, tokenResponse.CodeChallenge))
                        {
                            logger.LogWarning("PKCE verification failed for user {UserId}", tokenResponse.UserId);
                            return Results.Json(new LBaseResponse<object>("PKCE verification failed"), statusCode: StatusCodes.Status406NotAcceptable);
                        }
                        PkceService.DeleteVerifier(context.Response);
                    }
                }

                var userGuid = tokenResponse.UserId;
                var userName = tokenResponse.Username;
                var displayName = tokenResponse.DisplayName;

                // Calculate token expiration
                var accessTokenExpiry = tokenResponse.AccessTokenExpiresAt.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.AccessTokenExpiresAt.Value).UtcDateTime
                    : DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration);

                var hasIntegrations = tokenResponse.Integrations?.Count > 0;
                string localRefreshToken;

                if (hasIntegrations)
                {
                    // Integrations exist - use Linbik's refresh token
                    localRefreshToken = tokenResponse.RefreshToken ?? string.Empty;

                    // Store Linbik refresh token
                    context.Response.Cookies.Append(LinbikRefreshTokenCookie, localRefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = tokenResponse.RefreshTokenExpiresAt.HasValue
                            ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.RefreshTokenExpiresAt.Value).UtcDateTime
                            : DateTime.UtcNow.AddDays(options.RefreshTokenExpiration)
                    });

                    // Store integration tokens in separate cookies
                    foreach (var integration in tokenResponse.Integrations)
                    {
                        var integrationCookieName = $"{IntegrationTokenPrefix}{integration.PackageName}";
                        context.Response.Cookies.Append(integrationCookieName, integration.AccessToken, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.None,
                            Expires = accessTokenExpiry
                        });
                    }

                    // Mark that user has integrations
                    context.Response.Cookies.Append(HasIntegrationsCookie, "true", cookieOptions);
                }
                else
                {
                    // No integrations - create local refresh token
                    var (refreshToken, success) = await repository.CreateRefresToken(userGuid, userName).ConfigureAwait(false);
                    if (!success)
                        return Results.Json(new LBaseResponse<object>("Failed to create refresh token"), statusCode: StatusCodes.Status500InternalServerError);
                    localRefreshToken = refreshToken;
                    context.Response.Cookies.Append(HasIntegrationsCookie, "false", cookieOptions);
                }

                // Create local access token
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, userGuid.ToString()),
                    new(ClaimTypes.Name, userName),
                    new("display_name", displayName ?? userName)
                };

                var securityKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.PrivateKey ?? throw new InvalidOperationException("PrivateKey not configured")));
                var credentials = new SigningCredentials(securityKey, options.Algorithm ?? SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: options.JwtIssuer,
                    audience: options.JwtAudience,
                    claims: claims,
                    expires: accessTokenExpiry,
                    signingCredentials: credentials
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                // Store tokens in cookies
                context.Response.Cookies.Append(AuthTokenCookie, tokenString, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = accessTokenExpiry
                });

                context.Response.Cookies.Append(RefreshTokenCookie, localRefreshToken, cookieOptions);
                context.Response.Cookies.Append(UserNameCookie, userName, cookieOptions);

                return Results.Ok(new LBaseResponse<object>(new
                {
                    userId = userGuid,
                    userName,
                    displayName,
                    hasIntegrations,
                    integrations = tokenResponse.Integrations?.Select(i => i.PackageName).ToList() ?? new List<string>()
                }));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login failed");
                return Results.Json(new LBaseResponse<object>(ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        }).WithTags("Linbik.Auth").AllowAnonymous();

        // Logout endpoint
        endpoints.MapPost(options.ExitPath ?? "/linbik/logout", async (HttpContext context) =>
        {
            // Delete all auth cookies
            context.Response.Cookies.Delete(AuthTokenCookie);
            context.Response.Cookies.Delete(RefreshTokenCookie);
            context.Response.Cookies.Delete(LinbikRefreshTokenCookie);
            context.Response.Cookies.Delete(HasIntegrationsCookie);
            context.Response.Cookies.Delete(UserNameCookie);

            // Delete integration cookies
            foreach (var cookie in context.Request.Cookies)
            {
                if (cookie.Key.StartsWith(IntegrationTokenPrefix))
                {
                    context.Response.Cookies.Delete(cookie.Key);
                }
            }

            await Task.CompletedTask;
            return Results.Ok(new LBaseResponse<object>("Logged out successfully"));
        }).WithTags("Linbik.Auth");

        // Refresh endpoint
        endpoints.MapPost(options.RefreshPath ?? "/linbik/refresh", async (HttpContext context,
            [FromServices] ILinbikAuthClient linbikClient,
            [FromServices] ILinbikRepository repository,
            [FromServices] ILogger<ILinbikAuthClient> logger) =>
        {
            try
            {
                var hasIntegrationsCookie = context.Request.Cookies[HasIntegrationsCookie];
                var hasIntegrations = hasIntegrationsCookie?.ToLower() == "true";

                if (hasIntegrations)
                {
                    // Use Linbik refresh token
                    var linbikRefreshToken = context.Request.Cookies[LinbikRefreshTokenCookie];
                    if (string.IsNullOrEmpty(linbikRefreshToken))
                    {
                        return Results.Json(new LBaseResponse<object>("No refresh token available"), statusCode: StatusCodes.Status401Unauthorized);
                    }

                    var tokenResponse = await linbikClient.RefreshTokensAsync(linbikRefreshToken).ConfigureAwait(false);
                    if (tokenResponse is null)
                    {
                        // Clear cookies on refresh failure
                        context.Response.Cookies.Delete(AuthTokenCookie);
                        context.Response.Cookies.Delete(LinbikRefreshTokenCookie);
                        return Results.Json(new LBaseResponse<object>("Token refresh failed"), statusCode: StatusCodes.Status401Unauthorized);
                    }

                    var accessTokenExpiry = tokenResponse.AccessTokenExpiresAt.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.AccessTokenExpiresAt.Value).UtcDateTime
                        : DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration);

                    // Update integration tokens
                    if (tokenResponse.Integrations?.Count > 0)
                    {
                        foreach (var integration in tokenResponse.Integrations)
                        {
                            var integrationCookieName = $"{IntegrationTokenPrefix}{integration.PackageName}";
                            context.Response.Cookies.Append(integrationCookieName, integration.AccessToken, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.None,
                                Expires = accessTokenExpiry
                            });
                        }
                    }

                    // Update Linbik refresh token if new one provided
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                    {
                        context.Response.Cookies.Append(LinbikRefreshTokenCookie, tokenResponse.RefreshToken, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.None,
                            Expires = tokenResponse.RefreshTokenExpiresAt.HasValue
                                ? DateTimeOffset.FromUnixTimeSeconds(tokenResponse.RefreshTokenExpiresAt.Value).UtcDateTime
                                : DateTime.UtcNow.AddDays(options.RefreshTokenExpiration)
                        });
                    }

                    return Results.Ok(new LBaseResponse<object>(new
                    {
                        refreshed = true,
                        integrations = tokenResponse.Integrations?.Select(i => i.PackageName).ToList() ?? new List<string>()
                    }));
                }
                else
                {
                    // Local refresh token
                    var localRefreshToken = context.Request.Cookies[RefreshTokenCookie];
                    if (string.IsNullOrEmpty(localRefreshToken))
                    {
                        return Results.Json(new LBaseResponse<object>("No refresh token available"), statusCode: StatusCodes.Status401Unauthorized);
                    }

                    var tokenResult = await repository.UseRefresToken(localRefreshToken).ConfigureAwait(false);
                    if (!tokenResult.Success || tokenResult.UserGuid == Guid.Empty)
                    {
                        context.Response.Cookies.Delete(AuthTokenCookie);
                        context.Response.Cookies.Delete(RefreshTokenCookie);
                        return Results.Json(new LBaseResponse<object>("Invalid refresh token"), statusCode: StatusCodes.Status401Unauthorized);
                    }

                    var userGuid = tokenResult.UserGuid;
                    var userName = context.Request.Cookies[UserNameCookie] ?? "user";
                    var (newRefreshTokenValue, createSuccess) = await repository.CreateRefresToken(userGuid, userName).ConfigureAwait(false);
                    if (!createSuccess)
                        return Results.Json(new LBaseResponse<object>("Failed to create refresh token"), statusCode: StatusCodes.Status500InternalServerError);

                    var accessTokenExpiry = DateTime.UtcNow.AddMinutes(options.AccessTokenExpiration);
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, userGuid.ToString()),
                        new(ClaimTypes.Name, userName)
                    };

                    var securityKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.PrivateKey ?? throw new InvalidOperationException("PrivateKey not configured")));
                    var credentials = new SigningCredentials(securityKey, options.Algorithm ?? SecurityAlgorithms.HmacSha256);

                    var token = new JwtSecurityToken(
                        issuer: options.JwtIssuer,
                        audience: options.JwtAudience,
                        claims: claims,
                        expires: accessTokenExpiry,
                        signingCredentials: credentials
                    );

                    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                    context.Response.Cookies.Append(AuthTokenCookie, tokenString, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = accessTokenExpiry
                    });

                    context.Response.Cookies.Append(RefreshTokenCookie, newRefreshTokenValue, cookieOptions);

                    return Results.Ok(new LBaseResponse<object>(new { refreshed = true }));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Token refresh failed");
                return Results.Json(new LBaseResponse<object>(ex.Message), statusCode: StatusCodes.Status500InternalServerError);
            }
        }).WithTags("Linbik.Auth");

        return endpoints;
    }
}

