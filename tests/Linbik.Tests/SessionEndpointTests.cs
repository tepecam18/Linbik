using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Linbik.Core;
using Linbik.Core.Extensions;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Linbik.Tests;

public sealed class SessionEndpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionValidatesTokensWithoutRefreshingOrWritingCookies(bool paseto)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAuthorization();
        var helper = new PasetoHelperService();
        builder.Services.AddSingleton<IPasetoHelper>(helper);
        Claim[] claims = [new("sub", "user-123"), new("preferred_username", "alice"), new("name", "Alice")];
        string token;
        if (paseto)
        {
            var key = helper.GenerateSymmetricKey();
            builder.Services.AddAuthentication().AddLinbikPasetoBearer(LinbikDefaults.ClientScheme, options =>
            {
                options.Mode = PasetoMode.Local;
                options.SharedKey = key;
                options.ExpectedIssuer = "Linbik";
                options.ExpectedAudience = "test";
                options.RequireApplicationToken = false;
                options.TokenRetriever = request => request.Cookies[LinbikDefaults.AuthTokenCookie];
            });
            token = await helper.CreateLocalTokenAsync(claims, key, "test", 5);
        }
        else
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 64)));
            builder.Services.AddAuthentication().AddJwtBearer(LinbikDefaults.ClientScheme, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = "Linbik", ValidAudience = "test", IssuerSigningKey = key,
                    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true
                };
                options.Events = new JwtBearerEvents { OnMessageReceived = context =>
                {
                    context.Token = context.Request.Cookies[LinbikDefaults.AuthTokenCookie];
                    return Task.CompletedTask;
                }};
            });
            token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("Linbik", "test", claims,
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
        }
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapLinbikSession("/session");
        await app.StartAsync();
        using var client = app.GetTestClient();
        foreach (var cookie in new[] { "username=alice", "authToken=invalid", "authToken=" + token })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/session");
            request.Headers.Add("Cookie", cookie);
            using var response = await client.SendAsync(request);
            Assert.Equal(cookie.EndsWith(token) ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.Contains("user-123", body);
                Assert.Contains("Alice", body);
                Assert.DoesNotContain(token, body);
            }
        }
    }
}
