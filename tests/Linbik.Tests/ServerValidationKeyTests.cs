using System.Security.Claims;
using Linbik.Core.Configuration;
using Linbik.Core.Services;
using Linbik.Server.Configuration;
using Linbik.Server.Services;
using Microsoft.Extensions.Options;

namespace Linbik.Tests;

public sealed class ServerValidationKeyTests
{
    [Fact]
    public async Task StandardModeUsesOnlyCorePublicKey()
    {
        var helper = new PasetoHelperService();
        var keys = helper.GenerateKeyPair();
        var core = new LinbikOptions { KeylessMode = false, PublicKey = keys.PublicKeyBase64, PackageName = "service-x" };
        var server = new ServerOptions();
        Assert.True(new ServerOptionsValidator(Options.Create(core)).Validate(null, server).Succeeded);
        var validator = new IntegrationTokenValidator(Options.Create(server), helper, coreOptions: Options.Create(core));
        Assert.True(validator.IsConfigured());
        var token = await helper.CreateTokenAsync([new Claim("sub", "user")], keys.PrivateKeyBase64, "service-x");
        Assert.NotNull(validator.ValidateToken(token));
        core.PackageName = "other-service";
        Assert.Null(validator.ValidateToken(token));
        core.PackageName = "service-x";
        core.JwtIssuer = "other-issuer";
        Assert.Null(validator.ValidateToken(token));
        core.JwtIssuer = "Linbik";
        core.PublicKey = helper.GenerateKeyPair().PublicKeyBase64;
        Assert.Null(validator.ValidateToken(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-base64")]
    [InlineData("YWJj")]
    public void StandardModeRejectsMissingOrMalformedCoreKey(string key)
    {
        var core = new LinbikOptions { KeylessMode = false, PublicKey = key, PackageName = "service-x" };
        Assert.True(new ServerOptionsValidator(Options.Create(core)).Validate(null, new ServerOptions()).Failed);
    }
}
