using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Linbik.JwtAuthManager;

public static class AuthenticationBuilderExtensions
{
    public static AuthenticationBuilder AddLinbikScheme(this AuthenticationBuilder builder, IOptions<JwtAuthOptions> options)
    {
        return builder.AddJwtBearer("LinbikScheme", opt =>
        {
            if (!string.IsNullOrEmpty(options.Value.privateKey))
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.privateKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                };
            }
        });
    }
}

public class LinbikSchemeAttribute : AuthorizeAttribute
{
    public LinbikSchemeAttribute()
    {
        AuthenticationSchemes = "LinbikScheme";
    }
}
