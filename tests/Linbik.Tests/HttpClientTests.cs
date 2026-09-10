using System.Net;
using System.Reflection;
using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Services;
using Linbik.YARP.Configuration;
using Linbik.YARP.Interfaces;
using Linbik.YARP.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Linbik.Tests;

public sealed class HttpClientTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"isSuccess\":true,\"data\":null}")]
    [InlineData("{\"isSuccess\":true,\"data\":{}}")]
    [InlineData("{\"isSuccess\":false,\"data\":{\"userId\":\"22222222-2222-2222-2222-222222222222\",\"username\":\"test\"}}")]
    [InlineData("{\"isSuccess\":true,\"data\":{\"userId\":\"22222222-2222-2222-2222-222222222222\",\"username\":\"\"}}")]
    public async Task RefreshRejectsIncompleteOrUnsuccessfulEnvelope(string body)
    {
        using var http = new HttpClient(new CallbackHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })));
        var client = new LinbikAuthClient(http, Options.Create(new LinbikOptions { ServiceId = Guid.NewGuid().ToString() }), NullLogger<LinbikAuthClient>.Instance);
        Assert.Null(await client.RefreshTokensAsync("test-refresh"));
    }

    [Fact]
    public async Task CoreRequestKeepsApiKeyDiagnosticsAndJsonBody()
    {
        var clientId = Guid.NewGuid();
        using var http = new HttpClient(new CallbackHandler(async (request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("test-api-key", Assert.Single(request.Headers.GetValues("ApiKey")));
            Assert.Equal("sdk", Assert.Single(request.Headers.GetValues(Linbik.Core.LinbikDefaults.HeaderMode)));
            Assert.Equal("aspnet", Assert.Single(request.Headers.GetValues(Linbik.Core.LinbikDefaults.HeaderPlatform)));
            Assert.Contains(clientId.ToString(), await request.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"isSuccess":true,"data":{"token":"test","redirectUrl":"https://example.com/login"}}""")
            };
        }));
        var client = new LinbikAuthClient(http, Options.Create(new LinbikOptions { ApiKey = "test-api-key" }), NullLogger<LinbikAuthClient>.Instance);
        Assert.True((await client.InitiateAuthAsync(new LinbikInitiateRequest { ClientId = clientId })).IsSuccess);
    }

    [Fact]
    public async Task CorePreservesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var http = new HttpClient(new CallbackHandler((_, _) =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }));
        var client = new LinbikAuthClient(http, Options.Create(new LinbikOptions()), NullLogger<LinbikAuthClient>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.InitiateAuthAsync(
            new LinbikInitiateRequest { ClientId = Guid.NewGuid() }, cancellation.Token));
    }

    [Fact]
    public async Task ApplicationRequestsInjectTokenAndDisposeResponseContent()
    {
        var content = new TrackingContent("""{"isSuccess":true,"data":{"value":"ok"}}""");
        using var http = new HttpClient(new CallbackHandler((request, _) =>
        {
            Assert.Equal("https://example.com/api/items", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }));
        var client = CreateApplicationClient(http);
        var result = await client.GetAsync<Payload>("billing", "/items");
        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Data!.Value);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task ApplicationRequestsPreserveCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var http = new HttpClient(new CallbackHandler((_, _) =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateApplicationClient(http).GetAsync<Payload>("billing", "/items", cancellation.Token));
    }

    [Theory]
    [InlineData(200, "not-json")]
    [InlineData(502, "<html>bad gateway</html>")]
    public async Task InvalidResponsesProduceFailureEnvelope(int status, string body)
    {
        using var http = new HttpClient(new CallbackHandler((_, _) => Task.FromResult(
            new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) })));
        var result = await CreateApplicationClient(http).GetAsync<Payload>("billing", "/items");
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FriendlyMessage);
    }

    private static ApplicationServiceClient CreateApplicationClient(HttpClient http) => new(
        http, DispatchProxy.Create<IApplicationTokenProvider, FixedTokenProvider>(),
        Options.Create(new YARPOptions()), NullLogger<ApplicationServiceClient>.Instance);

    public class FixedTokenProvider : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            nameof(IApplicationTokenProvider.GetApplicationIntegrationAsync) => Task.FromResult<LinbikApplicationIntegration?>(new()
            { PackageName = "billing", Token = "test-token", ServiceUrl = "https://example.com/api" }),
            _ => throw new NotSupportedException(method?.Name)
        };
    }
    public sealed record Payload(string Value);
    private sealed class CallbackHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private sealed class TrackingContent(string value) : StringContent(value)
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
