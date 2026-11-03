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
            using var response = await client.SendAsync(request);
            Assert.Equal(expected, response.StatusCode);
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
