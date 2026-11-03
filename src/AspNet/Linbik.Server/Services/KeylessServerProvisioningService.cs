using Linbik.Core.Configuration;
using Linbik.Core.Services;
using Linbik.Server.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Linbik.Server.Services;

/// <summary>Provision receiving services even when they never initiate a user login.</summary>
internal sealed class KeylessServerProvisioningService(
    IServiceProvider services,
    IOptions<LinbikOptions> coreOptions,
    IServer server) : IHostedLifecycleService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        var opts = coreOptions.Value;
        if (!opts.KeylessMode || !string.IsNullOrWhiteSpace(opts.PublicKey) && !string.IsNullOrWhiteSpace(opts.PackageName))
            return;
        var client = opts.Clients.FirstOrDefault();
        var address = Uri.TryCreate(client?.RedirectUrl, UriKind.Absolute, out var configured) &&
            (configured.Scheme == "http" || configured.Scheme == "https")
            ? configured.GetLeftPart(UriPartial.Authority)
            : server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? "http://localhost";
        // Binding wildcards cannot be registered as callback hosts.
        address = address.Replace("://[::]", "://localhost").Replace("://0.0.0.0", "://localhost")
            .Replace("://*", "://localhost").Replace("://+", "://localhost");
        var provisionClient = services.GetService<LinbikProvisionClient>();
        if (provisionClient == null) return;
        await provisionClient.EnsureProvisionedAsync(address, "/api/linbik/callback", client?.Name, cancellationToken);
    }
}
