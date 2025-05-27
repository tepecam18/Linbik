using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Linbik.JwtAuthManager;

public static class AuthenticationBuilderExtensions
{
    public static AuthenticationBuilder AddLinbikScheme(this AuthenticationBuilder builder, IConfiguration config)
    {
        var options = config.GetSection("Linbik:Server").Get<JwtAuthOptions>();

        return builder.AddJwtBearer("LinbikScheme", opt =>
        {
            if (!string.IsNullOrEmpty(options?.privateKey))
            {
                opt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["AuthToken"];
                        return Task.CompletedTask;
                    }
                };

                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.privateKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                };

            }
        });
    }
}

public class LinbikAuthorizeAttribute : AuthorizeAttribute
{
    public LinbikAuthorizeAttribute()
    {
        AuthenticationSchemes = "LinbikScheme";
    }
}
