using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Linbik.Tests;

public sealed class AuthEndpointTests
{
    [Theory]
    [InlineData("/login", "/login?error=a%20%26%20b")]
    [InlineData("/login?next=home", "/login?next=home&error=a%20%26%20b")]
    [InlineData("/login#form", "/login?error=a%20%26%20b#form")]
    public void ErrorsPreserveExistingQueryAndFragment(string path, string expected)
    {
        var result = LinbikAuthEndpointHelpers.ReturnAuthError(null, path, "a & b");
        Assert.Equal(expected, Assert.IsType<RedirectHttpResult>(result).Url);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    public void MobileErrorsNeverRedirect(int statusCode)
    {
        var client = new LinbikClientConfig { ActionResultType = ActionResultType.Json };
        var result = LinbikAuthEndpointHelpers.ReturnAuthError(client, "/login", "failed", statusCode);
        Assert.IsNotType<RedirectHttpResult>(result);
        if (statusCode == 403)
            Assert.IsType<ForbidHttpResult>(result);
        else
            Assert.Equal(statusCode, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public void CookiesKeepTokenAndDisplayPoliciesSeparate()
    {
        var context = new DefaultHttpContext();
        var accessExpiry = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var refreshExpiry = accessExpiry.AddDays(14);
        LinbikAuthEndpointHelpers.SetAuthCookies(context, new LinbikTokenResponse
        {
            Username = "alice",
            RefreshToken = "refresh",
            Integrations = [new LinbikIntegrationToken { PackageName = "billing", Token = "integration" }]
        }, "access", accessExpiry, refreshExpiry, "example.com", SameSiteMode.Lax);
        var cookies = context.Response.Headers.SetCookie.Select(x => x!).ToArray();
        Assert.Equal(4, cookies.Length);
        Assert.All(cookies, cookie => { Assert.Contains("secure", cookie); Assert.Contains("samesite=lax", cookie); Assert.Contains("domain=example.com", cookie); });
        Assert.Equal(3, cookies.Count(x => x.Contains("httponly")));
        Assert.Contains(cookies, x => x.StartsWith("integration_billing=") && x.Contains("01 Jan 2030"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void MissingExpiryUsesFallback(long? timestamp)
    {
        var fallback = DateTime.UtcNow;
        Assert.Equal(fallback, LinbikAuthEndpointHelpers.CalculateExpiry(timestamp, fallback));
    }
}
