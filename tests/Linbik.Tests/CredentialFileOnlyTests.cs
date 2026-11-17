using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Linbik.Core;
using Linbik.Core.Builders;
using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Linbik.Server.Configuration;
using Linbik.Server.Extensions;
using Linbik.YARP.Services;
using Linbik.JwtAuthManager.Configuration;
using Linbik.JwtAuthManager.Extensions;
using Linbik.PasetoAuthManager.Configuration;
using Linbik.PasetoAuthManager.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Linbik.Tests;

// The real-file test changes the working directory used by FileLinbikCredentialStore.
[CollectionDefinition("Credential files", DisableParallelization = true)]
public sealed class CredentialFileCollection;

[Collection("Credential files")]
public sealed class CredentialFileOnlyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingMalformedAndIncompleteFilesAreRetriedWithoutGeneratingOrWriting(bool keyless)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var directory = Path.Combine(Path.GetTempPath(), "linbik-consumer-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            Directory.SetCurrentDirectory(directory);
            var options = new LinbikOptions { KeylessMode = keyless, CredentialFileOnly = true };
            Assert.True(new LinbikOptionsValidator().Validate(null, options).Succeeded);
            Assert.True(new ServerOptionsValidator(Options.Create(options)).Validate(null, new ServerOptions()).Succeeded);
            var store = new FileLinbikCredentialStore(NullLogger<FileLinbikCredentialStore>.Instance);
            // The consumer must never attempt provisioning or validation-key recovery.
            var factory = new ForbiddenHttpFactory();
            var provision = Provision(options, store, factory);
            await provision.EnsureProvisionedAsync("", "", null);
            Assert.False(Directory.Exists(".linbik"));
            Directory.CreateDirectory(".linbik");
            var path = Path.Combine(".linbik", "credentials.json");
            foreach (var content in new[] { "{", "{}", "{\"serviceId\":\"invalid\"}" })
            {
                await File.WriteAllTextAsync(path, content);
                await provision.EnsureProvisionedAsync("", "", null);
                Assert.Empty(options.ServiceId);
                Assert.Equal(content, await File.ReadAllTextAsync(path));
            }

            var credentials = Credentials();
            // Serialize with the same naming policy as the producer.
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            credentials.PublicKey = "invalid-key";
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(credentials, jsonOptions));
            await provision.EnsureProvisionedAsync("", "", null);
            Assert.Empty(options.ServiceId);
            credentials.PublicKey = new PasetoHelperService().GenerateKeyPair().PublicKeyBase64;
            var complete = JsonSerializer.Serialize(credentials, jsonOptions);
            await File.WriteAllTextAsync(path, complete);
            await provision.EnsureProvisionedAsync("", "", null);
            Assert.Equal(credentials.ServiceId, options.ServiceId);
            Assert.Equal(credentials.ApiKey, options.ApiKey);
            Assert.Equal(credentials.ClientId, Assert.Single(options.Clients).ClientId);
            await provision.HandleClaimAsync("replacement-key");
            Assert.Equal(credentials.ApiKey, options.ApiKey);
            Assert.Equal(complete, await File.ReadAllTextAsync(path));
            Assert.Equal(0, factory.Calls);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReceiverRecoversAfterStartupAndFailedAuthentication(bool keyless)
    {
        var helper = new PasetoHelperService();
        var keys = helper.GenerateKeyPair();
        var store = new ReadOnlyStore();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.Configure<LinbikOptions>(o => { o.KeylessMode = keyless; o.CredentialFileOnly = true; });
        builder.Services.AddSingleton<ILinbikCredentialStore>(store);
        var factory = new ForbiddenHttpFactory();
        builder.Services.AddSingleton<IHttpClientFactory>(factory);
        builder.Services.AddSingleton<LinbikProvisionClient>();
        builder.Services.AddSingleton<IPasetoHelper>(helper);
        new LinbikBuilder(builder.Services).AddLinbikServer();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/protected", () => "ok").RequireAuthorization(p =>
        {
            p.AddAuthenticationSchemes(LinbikDefaults.ApplicationScheme);
            p.RequireAuthenticatedUser();
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        var token = await helper.CreateTokenAsync([new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("token_type", "apps")], keys.PrivateKeyBase64, "consumer");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var missing = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        store.Credentials = new LinbikCredentials { ServiceId = Guid.NewGuid().ToString() };
        using var incomplete = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, incomplete.StatusCode);
        store.Credentials = Credentials(keys.PublicKeyBase64);
        using var ready = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(0, factory.Calls);
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingHttpClientAndAppsProviderRetryAfterFileArrives(bool dynamicTarget)
    {
        var options = new LinbikOptions { KeylessMode = false, CredentialFileOnly = true, AppsAutoRefresh = false };
        var store = new ReadOnlyStore();
        var factory = new ForbiddenHttpFactory();
        var provision = Provision(options, store, factory);
        var handler = new AppsTransport();
        using var http = new HttpClient(handler);
        var auth = new LinbikAuthClient(http, Options.Create(options), NullLogger<LinbikAuthClient>.Instance, provision);
        using var provider = new ApplicationTokenProvider(auth, Options.Create(options), provision,
            NullLogger<ApplicationTokenProvider>.Instance);
        var target = Guid.NewGuid();
        Task<LinbikApplicationIntegration?> Get() => dynamicTarget
            ? provider.GetApplicationIntegrationByIdAsync(target)
            : provider.GetApplicationIntegrationAsync("target");
        Assert.Null(await Get());
        Assert.Null(await auth.GetApplicationTokensAsync(new[] { "target" }));
        Assert.Equal(0, handler.Calls);
        store.Credentials = Credentials();
        handler.ExpectedApiKey = store.Credentials.ApiKey;
        handler.ExpectedServiceId = store.Credentials.ServiceId;
        Assert.NotNull(await Get());
        Assert.NotNull(await auth.GetApplicationTokensAsync(new[] { "target" }));
        Assert.Equal(2, handler.Calls);
        Assert.NotNull(await Get());
        Assert.Equal(2, handler.Calls);
        Assert.Equal(0, factory.Calls);
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConsumerNeverGeneratesLocalSigningKeys(bool paseto)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<LinbikOptions>(o => o.CredentialFileOnly = true);
        var builder = new LinbikBuilder(services);
        if (paseto) builder.AddLinbikPasetoAuth();
        else builder.AddLinbikJwtAuth();
        using var provider = services.BuildServiceProvider();
        if (paseto)
            Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<PasetoAuthOptions>>().Value);
        else
            Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JwtAuthOptions>>().Value);
    }

    [Fact]
    public async Task DisabledKeylessModeStillIgnoresFileUnlessConsumerIsEnabled()
    {
        var options = new LinbikOptions { KeylessMode = false };
        await Provision(options, new ReadOnlyStore { Credentials = Credentials() }).EnsureProvisionedAsync("", "", null);
        Assert.Empty(options.ServiceId);
        Assert.True(new LinbikOptionsValidator().Validate(null, options).Failed);
    }

    private static LinbikProvisionClient Provision(LinbikOptions options, ILinbikCredentialStore store, ForbiddenHttpFactory? factory = null) =>
        new(factory ?? new ForbiddenHttpFactory(), store, Options.Create(options), NullLogger<LinbikProvisionClient>.Instance);

    private static LinbikCredentials Credentials(string? publicKey = null) => new()
    {
        ServiceId = Guid.NewGuid().ToString(), ClientId = Guid.NewGuid().ToString(), ClientName = "Default",
        ApiKey = new string('a', 40), PackageName = "consumer",
        PublicKey = publicKey ?? new PasetoHelperService().GenerateKeyPair().PublicKeyBase64
    };

    private sealed class ForbiddenHttpFactory : IHttpClientFactory
    {
        public int Calls { get; private set; }
        public HttpClient CreateClient(string name)
        {
            Calls++;
            throw new InvalidOperationException("Consumer must not provision or recover keys.");
        }
    }

    private sealed class ReadOnlyStore : ILinbikCredentialStore
    {
        public LinbikCredentials? Credentials { get; set; }
        public int Writes { get; private set; }
        public Task<LinbikCredentials?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Credentials);
        public Task SaveAsync(LinbikCredentials credentials, CancellationToken cancellationToken = default)
        {
            Writes++;
            throw new InvalidOperationException("Read-only store");
        }
        public Task DeleteAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Read-only store");
        public Task<bool> HasValidCredentialsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Credentials != null);
    }

    private sealed class AppsTransport : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? ExpectedApiKey { get; set; }
        public string? ExpectedServiceId { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal(ExpectedApiKey, request.Headers.GetValues("ApiKey").Single());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal(ExpectedServiceId, body.RootElement.GetProperty("sourceServiceId").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                data = new { integrations = new[] { new { packageName = "target", token = "apps-token" } },
                    accessTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }
            }) };
        }
    }
}
