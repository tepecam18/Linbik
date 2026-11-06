using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Services;
using Linbik.Server.Configuration;
using Linbik.Server.Services;
using Linbik.YARP.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Linbik.Tests;

public sealed class ApplicationIntegrationIdentityTests
{
    private static readonly Guid SourceId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();

    [Fact]
    public async Task PackageLookupPreservesTargetIdThroughJsonAndCache()
    {
        using var fixture = new Fixture([Integration(TargetId)]);
        var integration = await fixture.Provider.GetApplicationIntegrationAsync("target");
        Assert.Equal(TargetId, integration!.ServiceId);
        Assert.NotEqual(SourceId, integration.ServiceId);
        Assert.Equal("opaque-token", integration.Token);
        Assert.Same(integration, await fixture.Provider.GetApplicationIntegrationAsync("target"));
        Assert.Equal(1, fixture.Handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyPackageLookupOnlyUsesExplicitMappingForTargetIdentity(bool configured)
    {
        using var fixture = new Fixture([Integration()], configured);
        var integration = await fixture.Provider.GetApplicationIntegrationAsync("target");
        Assert.Equal(configured ? TargetId : Guid.Empty, integration!.ServiceId);
        Assert.NotEqual(SourceId, integration.ServiceId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdLookupReturnsRequestedIdIncludingLegacyResponseAndCache(bool hasId)
    {
        using var fixture = new Fixture([Integration(hasId ? TargetId : null)]);
        var integration = await fixture.Provider.GetApplicationIntegrationByIdAsync(TargetId);
        Assert.Equal(TargetId, integration!.ServiceId);
        Assert.Same(integration, await fixture.Provider.GetApplicationIntegrationByIdAsync(TargetId));
        Assert.Equal(1, fixture.Handler.Calls);
    }

    [Fact]
    public async Task IdLookupDoesNotAssociateAnUnrelatedServiceWithRequestedId()
    {
        using var fixture = new Fixture([Integration(Guid.NewGuid(), "unrelated")]);
        Assert.Null(await fixture.Provider.GetApplicationIntegrationByIdAsync(TargetId));
    }

    [Fact]
    public async Task IdLookupSelectsMatchingIdentityInsteadOfFirstIntegration()
    {
        using var fixture = new Fixture([Integration(Guid.NewGuid(), "unrelated"), Integration(TargetId)]);
        var integration = await fixture.Provider.GetApplicationIntegrationByIdAsync(TargetId);
        Assert.Equal("target", integration!.PackageName);
        Assert.Equal(TargetId, integration.ServiceId);
    }

    [Fact]
    public async Task AmbiguousLegacyResponseCannotBeAssignedToRequestedIdentity()
    {
        using var fixture = new Fixture([Integration(null, "one"), Integration(null, "two")]);
        Assert.Null(await fixture.Provider.GetApplicationIntegrationByIdAsync(TargetId));
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("different-caller", false)]
    [InlineData("wrong-audience", false)]
    [InlineData("wrong-signature", false)]
    public async Task ReceiverChecksValidatedSourceIdentityAgainstExpectedPeer(string scenario, bool expected)
    {
        var helper = new PasetoHelperService();
        var keys = helper.GenerateKeyPair();
        var core = new LinbikOptions { PublicKey = keys.PublicKeyBase64, PackageName = "receiver" };
        var validator = new IntegrationTokenValidator(Options.Create(new ServerOptions()), helper,
            coreOptions: Options.Create(core));
        var caller = scenario == "different-caller" ? Guid.NewGuid() : SourceId;
        var token = await helper.CreateTokenAsync([
            new Claim("token_type", "apps"), new Claim("sub", caller.ToString()),
            new Claim("source_package_name", "sender")],
            scenario == "wrong-signature" ? helper.GenerateKeyPair().PrivateKeyBase64 : keys.PrivateKeyBase64,
            scenario == "wrong-audience" ? "another-target" : "receiver");
        var claims = validator.ValidateToken(token);
        var allowed = claims is { TokenType: Linbik.Server.Models.LinbikTokenType.Application } && claims.SourceServiceId == SourceId;
        Assert.Equal(expected, allowed);
        if (scenario == "valid") Assert.Equal(SourceId, claims!.SourceServiceId);
    }

    private static JsonObject Integration(Guid? id = null, string package = "target")
    {
        var result = new JsonObject { ["packageName"] = package, ["serviceName"] = "Target", ["serviceUrl"] = "https://target.test", ["token"] = "opaque-token" };
        if (id.HasValue) result["serviceId"] = id.Value.ToString();
        return result;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient http;
        public readonly ResponseHandler Handler;
        public readonly ApplicationTokenProvider Provider;
        public Fixture(JsonObject[] integrations, bool configured = false)
        {
            var options = new LinbikOptions { KeylessMode = false, ServiceId = SourceId.ToString(), AppsAutoRefresh = false };
            if (configured) options.AppsTargetServices["target"] = TargetId;
            Handler = new ResponseHandler(integrations);
            http = new HttpClient(Handler) { BaseAddress = new Uri("https://identity.test/") };
            var client = new LinbikAuthClient(http, Options.Create(options), NullLogger<LinbikAuthClient>.Instance);
            var provision = new LinbikProvisionClient(null!, null!, Options.Create(options), NullLogger<LinbikProvisionClient>.Instance);
            Provider = new ApplicationTokenProvider(client, Options.Create(options), provision, NullLogger<ApplicationTokenProvider>.Instance);
        }
        public void Dispose() { Provider.Dispose(); http.Dispose(); }
    }

    private sealed class ResponseHandler(JsonObject[] integrations) : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            Assert.Equal(SourceId.ToString(), body["sourceServiceId"]!.GetValue<string>());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
            { isSuccess = true, data = new { integrations, accessTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() } }) };
        }
    }
}
