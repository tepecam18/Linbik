using System.Reflection;
using System.Text.Json.Nodes;
using Linbik.Core.Configuration;
using Linbik.Core.Identity;
using Linbik.Core.Models;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.PasetoAuthManager.Configuration;
using Linbik.YARP.OpenApi;
using Linbik.YARP.Services;
using Linbik.Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Linbik.Tests;

public sealed class ThrowCastBoundaryTests
{
    private static JsonObject Document() => JsonNode.Parse("""{"openapi":"3.1.0","paths":{"/orders":{"get":{"responses":{"200":{"description":"OK"}}}}}}""")!.AsObject();

    [Theory]
    [InlineData("paths")]
    [InlineData("components")]
    [InlineData("schemas")]
    public void MalformedHostShapeReportsItsFieldAndDoesNotMutateDocuments(string field)
    {
        var host = Document();
        var source = Document();
        if (field == "schemas")
        {
            host["components"] = new JsonObject { ["schemas"] = new JsonArray() };
            source["components"] = new JsonObject { ["schemas"] = new JsonObject { ["Order"] = new JsonObject() } };
        }
        else if (field == "components")
        {
            host[field] = new JsonArray();
            source[field] = new JsonObject { ["schemas"] = new JsonObject() };
        }
        else host[field] = new JsonArray();
        var originalHost = host.ToJsonString();
        var originalSource = source.ToJsonString();
        var error = Assert.Throws<InvalidOperationException>(() => DelegatedDocumentMerger.Merge(host, source, "orders", "/api/orders"));
        Assert.Contains("host." + (field == "schemas" ? "components.schemas" : field), error.Message);
        Assert.Equal(originalHost, host.ToJsonString());
        Assert.Equal(originalSource, source.ToJsonString());
    }

    [Theory]
    [InlineData("{\"openapi\":3,\"paths\":{}}", "OpenAPI 3")]
    [InlineData("{\"openapi\":\"3.1.0\",\"paths\":{},\"components\":[]}", "components")]
    [InlineData("{\"openapi\":\"3.1.0\",\"paths\":{\"/x\":{\"get\":{\"operationId\":42}}}}", "operationId")]
    [InlineData("{\"openapi\":\"3.1.0\",\"paths\":{\"/x\":{\"$ref\":42}}}", "$ref")]
    [InlineData("{\"openapi\":\"3.1.0\",\"paths\":{},\"components\":{\"schemas\":{\"X\":{\"discriminator\":{\"propertyName\":\"kind\",\"mapping\":{\"x\":null}}}}}}", "discriminator.mapping.x")]
    public void MalformedDownstreamShapesHaveContractErrorsInsteadOfCastOrNullErrors(string json, string field)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            DelegatedDocumentMerger.Merge(Document(), JsonNode.Parse(json)!.AsObject(), "orders", "/api/orders"));
        Assert.Contains(field, error.Message);
    }

    [Fact]
    public void NullActorHasAnArgumentErrorInsteadOfNullReference()
    {
        Assert.Throws<ArgumentNullException>(() => LActorExtensions.Identity(null!));
        Assert.Throws<ArgumentNullException>(() => LActor.FromGatewayContext(null!));
    }

    [Fact]
    public void DuplicateKeyIdsReportConfigPathWithoutKeyMaterial()
    {
        var options = Options.Create(new PasetoKeyRingOptions { Keys = [
            new() { KeyId = "duplicate", PrivateKeyBase64 = "private-secret" },
            new() { KeyId = "duplicate" }] });
        var error = Assert.Throws<InvalidOperationException>(() => new PasetoKeyRing(options));
        Assert.Contains("Linbik:PasetoKeyRing:Keys", error.Message);
        Assert.Contains("unique KeyId", error.Message);
        Assert.DoesNotContain("private-secret", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredOrUnrepresentablePasetoLifetimeIsRejectedBeforeHelperCalls(bool expired)
    {
        var helper = DispatchProxy.Create<IPasetoHelper, UnusedPasetoHelper>();
        var type = typeof(PasetoAuthOptions).Assembly.GetType("Linbik.PasetoAuthManager.Services.LocalPasetoTokenIssuer")!;
        var method = type.GetMethod("CreateAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (Task<string?>)method.Invoke(null, [new PasetoAuthOptions { PrivateKeyBase64 = "not-used" },
            new LinbikTokenResponse { UserId = Guid.NewGuid(), Username = "user" },
            expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.MaxValue,
            helper, NullLogger.Instance])!;
        Assert.Null(await result);
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(-1, 0)]
    [InlineData(2147483648d, int.MaxValue)]
    public async Task RetryAfterRoundsUpAndCannotOverflow(double seconds, int expected)
    {
        using var services = new ServiceCollection().AddLinbikRateLimiting().BuildServiceProvider();
        var options = services.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        using var lease = new RetryLease(TimeSpan.FromSeconds(seconds));
        await options.OnRejected!(new OnRejectedContext { HttpContext = context, Lease = lease }, default);
        Assert.Equal(expected.ToString(), context.Response.Headers.RetryAfter.ToString());
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(75)]
    public void InvalidRefreshThresholdIsExplainedBeforeTimerOrTickConversions(double threshold)
    {
        var options = new LinbikOptions { KeylessMode = true, AppsRefreshThreshold = threshold };
        var validation = new LinbikOptionsValidator().Validate(null, options);
        Assert.True(validation.Failed);
        Assert.Contains(validation.Failures, f => f.Contains("AppsRefreshThreshold") && f.Contains("0.75"));
        var provision = new LinbikProvisionClient(null!, null!, Options.Create(options), NullLogger<LinbikProvisionClient>.Instance);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new ApplicationTokenProvider(
            DispatchProxy.Create<ILinbikAuthClient, UnusedPasetoHelper>(), Options.Create(options), provision,
            NullLogger<ApplicationTokenProvider>.Instance));
        Assert.Contains("AppsRefreshThreshold", error.Message);
    }

    private sealed class RetryLease(TimeSpan retry) : RateLimitLease
    {
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = metadataName == MetadataName.RetryAfter.Name ? retry : null;
            return metadata is not null;
        }
    }

    public class UnusedPasetoHelper : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new Exception("Invalid token lifetime must not reach signing or encryption.");
    }
}
