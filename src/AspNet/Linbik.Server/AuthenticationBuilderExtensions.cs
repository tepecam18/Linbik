using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Linbik.Server;

public static class AuthenticationBuilderExtensions
{
    public static AuthenticationBuilder AddLinbikAppScheme(this AuthenticationBuilder builder, IConfiguration config)
    {
        var options = config.GetSection("Linbik:Server").Get<ServerOptions>();


        return builder.AddJwtBearer("LinbikAppScheme", opt =>
        {
            if (!string.IsNullOrEmpty(options?.privateKey))
            {
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

public class LinbikAppAuthorizeAttribute : AuthorizeAttribute
{
    public LinbikAppAuthorizeAttribute()
    {
        AuthenticationSchemes = "LinbikAppScheme";
    }
}
