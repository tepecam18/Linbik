using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Linbik.Core;
using Linbik.Core.Builders;
using Linbik.Core.Configuration;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.Server.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Linbik.Tests;

public sealed class KeylessServerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReceivesApplicationTokensWithoutLoginAndRecoversOldCache(bool oldCache)
    {
        var helper = new PasetoHelperService();
        var (privateKey, publicKey) = helper.GenerateKeyPair();
        var serviceId = Guid.NewGuid().ToString();
        var store = new MemoryStore { Credentials = oldCache ? new LinbikCredentials
        {
            ServiceId = serviceId, ClientId = Guid.NewGuid().ToString(), ApiKey = "own-api-key", ClientName = "Default"
        } : null };
        var transport = new ProvisionTransport(serviceId, publicKey, oldCache);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.Configure<LinbikOptions>(o => o.KeylessMode = true);
        builder.Services.AddSingleton<ILinbikCredentialStore>(store);
        builder.Services.AddSingleton<LinbikProvisionClient>();
        builder.Services.AddSingleton<IPasetoHelper>(helper);
        var events = new RecordingSink();
        builder.Services.AddSingleton<ILinbikSecurityEventSink>(events);
        builder.Services.AddHttpClient("LinbikAuthClient", c => c.BaseAddress = new Uri("https://linbik.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        new LinbikBuilder(builder.Services).AddLinbikServer();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/callback", () => "ok").RequireAuthorization(p =>
        {
            p.AddAuthenticationSchemes(LinbikDefaults.ApplicationScheme);
            p.RequireAuthenticatedUser();
        });
        await app.StartAsync();
        // No login or outgoing apps call was needed to establish this service's keys.
        Assert.Single(transport.Paths);
        Assert.Equal(oldCache ? $"/api/dev/{serviceId}/validation-key" : "/api/dev/provision", transport.Paths[0]);
        Assert.Equal(publicKey, store.Credentials!.PublicKey);
        Assert.Equal("service-x", store.Credentials.PackageName);
        Assert.Equal(serviceId, store.Credentials.ServiceId);

        using var client = app.GetTestClient();
        foreach (var (audience, application, expected) in new[]
        {
            ("service-x", true, HttpStatusCode.OK),
            ("service-y", true, HttpStatusCode.Unauthorized),
            ("service-x", false, HttpStatusCode.Unauthorized)
        })
        {
            var token = await helper.CreateTokenAsync([
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("token_type", application ? "apps" : "user")], privateKey, audience);
            using var request = new HttpRequestMessage(HttpMethod.Get, "/callback");
            request.Headers.Authorization = new("Bearer", token);
            events.Events.Clear();
            using var response = await client.SendAsync(request);
            Assert.Equal(expected, response.StatusCode);
            if (expected == HttpStatusCode.Unauthorized)
            {
                var failure = Assert.Single(events.Events);
                Assert.DoesNotContain(token, failure.Message);
                Assert.DoesNotContain("Missing or unreadable", failure.Message);
                if (audience == "service-y") Assert.Contains("audience mismatch", failure.Message);
            }
            else Assert.Empty(events.Events);
        }
        Assert.Single(transport.Paths);
        // A subsequent process reuses the completed cache instead of creating another service.
        var restartedOptions = new LinbikOptions { KeylessMode = true };
        var restarted = new LinbikProvisionClient(app.Services.GetRequiredService<IHttpClientFactory>(), store,
            Options.Create(restartedOptions), NullLogger<LinbikProvisionClient>.Instance);
        await restarted.EnsureProvisionedAsync("https://x.test", "/api/linbik/callback", "Default");
        Assert.Single(transport.Paths);
        Assert.Equal(serviceId, restartedOptions.ServiceId);
        Assert.Equal(publicKey, restartedOptions.PublicKey);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExplicitPackageNameSurvivesProvisioningAndCacheRecovery(bool cached, bool recovery)
    {
        var serviceId = Guid.NewGuid().ToString();
        var store = new MemoryStore { Credentials = cached ? new LinbikCredentials
        {
            ServiceId = serviceId, ApiKey = "own-api-key", ClientId = Guid.NewGuid().ToString(),
            PackageName = recovery ? "" : "service-x", PublicKey = recovery ? "" : "cached-key"
        } : null };
        var transport = new ProvisionTransport(serviceId, "recovered-key", recovery);
        var services = new ServiceCollection();
        services.AddHttpClient("LinbikAuthClient", c => c.BaseAddress = new Uri("https://linbik.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        using var provider = services.BuildServiceProvider();
        var options = new LinbikOptions { KeylessMode = true, PackageName = "explicit-package" };
        var provision = new LinbikProvisionClient(provider.GetRequiredService<IHttpClientFactory>(), store,
            Options.Create(options), NullLogger<LinbikProvisionClient>.Instance);
        await provision.EnsureProvisionedAsync("https://app.test", "/callback", "Default");
        Assert.Equal("explicit-package", options.PackageName);
        Assert.Equal("service-x", store.Credentials!.PackageName);
        Assert.Equal(cached && !recovery ? 0 : 1, transport.Paths.Count);
    }

    [Theory]
    [InlineData(PasetoMode.Public)]
    [InlineData(PasetoMode.Local)]
    public async Task ValidationDiagnosticsDistinguishFailures(PasetoMode mode)
    {
        var helper = new PasetoHelperService();
        var (privateKey, publicKey) = helper.GenerateKeyPair();
        var key = mode == PasetoMode.Local ? helper.GenerateSymmetricKey() : publicKey;
        async Task<string> Token(int minutes = 60) => mode == PasetoMode.Local
            ? await helper.CreateLocalTokenAsync([], key, "service-x", minutes)
            : await helper.CreateTokenAsync([], privateKey, "service-x", minutes);
        var token = await Token();
        Assert.Null(await helper.GetValidationFailureAsync(token, key, "service-x", LinbikDefaults.Issuer, mode));
        Assert.Contains("audience mismatch", await helper.GetValidationFailureAsync(token, key, "other", LinbikDefaults.Issuer, mode));
        Assert.Contains("issuer mismatch", await helper.GetValidationFailureAsync(token, key, "service-x", "other", mode));
        Assert.Contains("expired", await helper.GetValidationFailureAsync(await Token(-10), key, "service-x", LinbikDefaults.Issuer, mode));
        Assert.Contains("Base64", await helper.GetValidationFailureAsync(token, "invalid!", "service-x", LinbikDefaults.Issuer, mode));
        Assert.Contains("signature or decryption", await helper.GetValidationFailureAsync(token, helper.GenerateSymmetricKey(), "service-x", LinbikDefaults.Issuer, mode));
    }

    private sealed class RecordingSink : ILinbikSecurityEventSink
    {
        public List<LinbikSecurityEvent> Events { get; } = [];
        public Task ReportAsync(LinbikSecurityEvent securityEvent, CancellationToken cancellationToken = default)
        { Events.Add(securityEvent); return Task.CompletedTask; }
    }

    private sealed class ProvisionTransport(string serviceId, string publicKey, bool recovery) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            if (recovery) Assert.Equal("own-api-key", request.Headers.GetValues("apiKey").Single());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { data = new
                {
                    serviceId, clientId = Guid.NewGuid(), apiKey = "own-api-key", packageName = "service-x", publicKey
                } })
            });
        }
    }

    private sealed class MemoryStore : ILinbikCredentialStore
    {
        public LinbikCredentials? Credentials { get; set; }
        public Task<LinbikCredentials?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Credentials);
        public Task SaveAsync(LinbikCredentials credentials, CancellationToken cancellationToken = default)
        { Credentials = credentials; return Task.CompletedTask; }
        public Task DeleteAsync(CancellationToken cancellationToken = default)
        { Credentials = null; return Task.CompletedTask; }
        public Task<bool> HasValidCredentialsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Credentials != null);
    }
}
