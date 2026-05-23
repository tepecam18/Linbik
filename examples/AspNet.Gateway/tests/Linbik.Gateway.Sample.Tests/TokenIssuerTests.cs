using System.Security.Claims;
using Linbik.Core.Services;
using Xunit;

namespace Linbik.Gateway.Sample.Tests;

/// <summary>
/// PASETO token üretici aracın temel davranışı.
/// </summary>
public class TokenIssuerTests
{
    [Fact]
    public void GenerateKeyPair_returns_base64_keys()
    {
        var helper = new PasetoHelperService();

        var (priv, pub) = helper.GenerateKeyPair();

        Assert.False(string.IsNullOrWhiteSpace(priv));
        Assert.False(string.IsNullOrWhiteSpace(pub));
        // Public key 32 byte → Base64 ≈ 44 char
        Assert.InRange(pub.Length, 40, 48);
    }

    [Fact]
    public async Task Issued_token_can_be_validated_with_matching_public_key()
    {
        var helper = new PasetoHelperService();
        var (priv, pub) = helper.GenerateKeyPair();

        var claims = new[]
        {
            new Claim("sub", "user-1"),
            new Claim("role", "admin")
        };

        var token = await helper.CreateTokenAsync(claims, priv, audience: "linbik-client", expirationMinutes: 5);
        var valid = await helper.ValidateTokenAsync(token, pub, expectedAudience: "linbik-client");

        Assert.True(valid);
    }

    [Fact]
    public async Task Token_with_wrong_audience_fails_validation()
    {
        var helper = new PasetoHelperService();
        var (priv, pub) = helper.GenerateKeyPair();

        var claims = new[] { new Claim("sub", "user-1") };
        var token = await helper.CreateTokenAsync(claims, priv, audience: "linbik-client");

        var valid = await helper.ValidateTokenAsync(token, pub, expectedAudience: "linbik-apps");

        Assert.False(valid);
    }
}
