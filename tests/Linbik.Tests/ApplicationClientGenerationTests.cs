using System.Net;
using System.Reflection;
using Linbik.Core.Models;
using Linbik.YARP.Interfaces;
using Linbik.Core.Builders;
using Linbik.YARP.Configuration;
using Linbik.YARP.Extensions;
using Linbik.YARP.Generated;
using Linbik.YARP.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Linbik.Tests
{
    public sealed class ApplicationClientGenerationTests
    {
        [Theory]
        [InlineData("/openapi/apps.json", 401, "app-token", 200, 2, true)]
        [InlineData("/openapi/delegated.json", 401, "app-token", 200, 2, true)]
        [InlineData("/openapi/apps.json", 200, null, 200, 1, true)]
        [InlineData("/openapi/apps.json", 401, null, 200, 1, false)]
        [InlineData("/openapi/apps.json", 401, "app-token", 401, 2, false)]
        [InlineData("/openapi/apps.json", 403, "app-token", 200, 1, false)]
        public async Task DocumentProbeUsesTargetApplicationTokenOnlyAfterChallenge(
            string documentPath, int initialStatus, string? token, int retryStatus, int expectedRequests, bool generated)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var output = Path.Combine(directory, "CustomApplicationClient.g.cs");
                await File.WriteAllTextAsync(output, "existing");
                var options = new YARPOptions { GeneratedClientOutputDirectory = directory };
                options.IntegrationServices.Add("messtick", new IntegrationServiceOptions
                {
                    TargetBaseUrl = "https://example.invalid", DocumentPath = documentPath, DocumentName = "custom"
                });
                var tokens = DispatchProxy.Create<IApplicationTokenProvider, ProbeTokens>();
                var tokenState = (ProbeTokens)tokens;
                tokenState.Token = token;
                var requests = new List<(string Path, string? Authorization)>();
                using var http = new HttpClient(new RecordingHandler(request =>
                {
                    requests.Add((request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString()));
                    return new HttpResponseMessage((HttpStatusCode)(requests.Count == 1 ? initialStatus : retryStatus))
                    {
                        Content = new StringContent("""{"openapi":"3.0.0","info":{"title":"Test","version":"1"},"paths":{}}""")
                    };
                }));
                var service = new ApplicationClientGenerationHostedService(new ProbeFactory(http),
                    Options.Create(options), WebApplication.CreateBuilder().Environment,
                    NullLogger<ApplicationClientGenerationHostedService>.Instance, tokens);
                await service.StartAsync(CancellationToken.None);
                Assert.Equal(expectedRequests, requests.Count);
                Assert.All(requests, request => Assert.Equal(documentPath, request.Path));
                Assert.Null(requests[0].Authorization);
                if (expectedRequests == 2) Assert.Equal("Bearer app-token", requests[1].Authorization);
                Assert.Null(http.DefaultRequestHeaders.Authorization);
                Assert.Equal(initialStatus == 401 ? new[] { "messtick" } : Array.Empty<string>(), tokenState.Packages);
                Assert.Equal(generated, await File.ReadAllTextAsync(output) != "existing");
            }
            finally { Directory.Delete(directory, recursive: true); }
        }

        public class ProbeTokens : DispatchProxy
        {
            public string? Token { get; set; }
            public List<string> Packages { get; } = [];
            protected override object? Invoke(MethodInfo? method, object?[]? args)
            {
                if (method?.Name != nameof(IApplicationTokenProvider.GetApplicationIntegrationAsync))
                    throw new NotSupportedException();
                Packages.Add((string)args![0]!);
                return Task.FromResult<LinbikApplicationIntegration?>(Token is null ? null : new()
                { PackageName = (string)args[0]!, Token = Token });
            }
        }

        private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(send(request));
        }

        [Fact]
        public void ExplicitClientRegistrationSupportsCustomTypesWithoutDocumentGeneration()
        {
            var services = new ServiceCollection();
            services.AddLinbikApplicationClient<ICustomClient, CustomClient>("messtick");
            using var provider = services.BuildServiceProvider();
            Assert.True(provider.GetRequiredService<IServiceProviderIsService>().IsService(typeof(ICustomClient)));
            RequestDelegateFactory.Create(
                (Input input, ICustomClient messtick) => Results.Ok(input),
                new RequestDelegateFactoryOptions { ServiceProvider = provider });
        }

        public interface ICustomClient;
        public sealed class CustomClient(HttpClient httpClient) : ICustomClient
        {
            public HttpClient HttpClient { get; } = httpClient;
        }

        [Fact]
        public async Task UnauthorizedDocumentPreservesClientAndEndpointServiceInference()
        {
            var builder = WebApplication.CreateBuilder();
            new LinbikBuilder(builder.Services).AddLinbikYarp(options =>
                options.IntegrationServices.Add("regression", new IntegrationServiceOptions
                {
                    TargetBaseUrl = "https://example.invalid",
                    SourcePath = "/regression"
                }));
            using var provider = builder.Services.BuildServiceProvider();
            var serviceTypes = provider.GetRequiredService<IServiceProviderIsService>();
            Assert.True(serviceTypes.IsService(typeof(RegressionApplicationClient)));
            Assert.True(serviceTypes.IsService(typeof(IRegressionApplicationClient)));

            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "RegressionApplicationClient.g.cs");
            try
            {
                await File.WriteAllTextAsync(path, "existing client source");
                var options = new YARPOptions { GeneratedClientOutputDirectory = directory };
                options.IntegrationServices.Add("regression", new IntegrationServiceOptions
                {
                    TargetBaseUrl = "https://example.invalid"
                });
                using var http = new HttpClient(new UnauthorizedHandler());
                var service = new ApplicationClientGenerationHostedService(
                    new ProbeFactory(http), Options.Create(options), builder.Environment,
                    NullLogger<ApplicationClientGenerationHostedService>.Instance);
                await service.StartAsync(CancellationToken.None);
                Assert.Equal("existing client source", await File.ReadAllTextAsync(path));

                RequestDelegateFactory.Create(
                    (Input input, RegressionApplicationClient regression) => Results.Ok(input),
                    new RequestDelegateFactoryOptions { ServiceProvider = provider });
                RequestDelegateFactory.Create(
                    (Input input, IRegressionApplicationClient regression) => Results.Ok(input),
                    new RequestDelegateFactoryOptions { ServiceProvider = provider });
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        public sealed record Input(string Value);
        private sealed class ProbeFactory(HttpClient client) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => client;
        }
        private sealed class UnauthorizedHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
    }
}

namespace Linbik.YARP.Generated
{
    public interface IRegressionApplicationClient;
    public sealed class RegressionApplicationClient(HttpClient httpClient) : IRegressionApplicationClient
    {
        public HttpClient HttpClient { get; } = httpClient;
    }
}
