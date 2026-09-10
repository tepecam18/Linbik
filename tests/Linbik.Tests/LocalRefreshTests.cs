using System.Reflection;
using Linbik.Core.Models;
using Linbik.Core.Services.Interfaces;
using Linbik.PasetoAuthManager.Services;

namespace Linbik.Tests;

public class LocalRefreshTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalRefreshRotatesAndReplayRevokesReplacement(bool jwt)
    {
        dynamic store = jwt ? (object)new Linbik.JwtAuthManager.Services.InMemoryLinbikRefreshTokenStore() : new InMemoryLinbikRefreshTokenStore();
        using var disposable = (IDisposable)store;
        dynamic manager = jwt ? (object)new Linbik.JwtAuthManager.Services.LinbikRefreshTokenManager(store) : new LinbikRefreshTokenManager(store);
        var response = new LinbikTokenResponse { UserId = Guid.NewGuid(), Username = "test" };
        var client = DispatchProxy.Create<ILinbikAuthClient, Upstream>();
        await manager.EnsureAsync(response, DateTime.UtcNow.AddHours(1), CancellationToken.None);
        var original = response.RefreshToken!;
        LinbikTokenResponse rotated = await manager.RefreshAsync(original, client, CancellationToken.None);
        Assert.Equal(response.UserId, rotated.UserId);
        Assert.NotEqual(original, rotated.RefreshToken);
        Assert.Equal(response.RefreshTokenExpiresAt, rotated.RefreshTokenExpiresAt);
        Assert.Null((object?)await manager.RefreshAsync(original, client, CancellationToken.None));
        Assert.Null((object?)await manager.RefreshAsync(rotated.RefreshToken!, client, CancellationToken.None));
        Assert.Equal(0, ((Upstream)client).Calls);
    }

    [Fact]
    public async Task LogoutRevokesLocalSession()
    {
        using var store = new InMemoryLinbikRefreshTokenStore();
        var manager = new LinbikRefreshTokenManager(store);
        var response = new LinbikTokenResponse { Username = "test" };
        await manager.EnsureAsync(response, DateTime.UtcNow.AddHours(1));
        await manager.RevokeAsync(response.RefreshToken);
        Assert.Null(await manager.RefreshAsync(response.RefreshToken!, DispatchProxy.Create<ILinbikAuthClient, Upstream>()));
    }

    [Fact]
    public async Task UpstreamTokenIsPreservedAndFailureNeverCreatesLocalSession()
    {
        using var store = new InMemoryLinbikRefreshTokenStore();
        var manager = new LinbikRefreshTokenManager(store);
        var response = new LinbikTokenResponse { RefreshToken = "upstream-test-token" };
        await manager.EnsureAsync(response, DateTime.UtcNow.AddHours(1));
        Assert.Equal("upstream-test-token", response.RefreshToken);
        var client = DispatchProxy.Create<ILinbikAuthClient, Upstream>();
        Assert.Null(await manager.RefreshAsync(response.RefreshToken, client));
        Assert.Equal(1, ((Upstream)client).Calls);
    }

    [Fact]
    public async Task StoreRejectsExpiredSessionsAndConcurrentReuse()
    {
        using var store = new InMemoryLinbikRefreshTokenStore();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.CreateAsync("expired", new(Guid.NewGuid(), "test", null, DateTimeOffset.UtcNow.AddMinutes(-1))));
        await store.CreateAsync("old", new(Guid.NewGuid(), "test", null, DateTimeOffset.UtcNow.AddHours(1)));
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Task.Run(() => store.RotateAsync("old", "next" + i))));
        Assert.Single(results, r => r is not null);
        for (var i = 0; i < 10; i++) Assert.Null(await store.RotateAsync("next" + i, "later"));
    }

    public class Upstream : DispatchProxy
    {
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.Equal(nameof(ILinbikAuthClient.RefreshTokensAsync), targetMethod!.Name);
            Calls++;
            return Task.FromResult<LinbikTokenResponse?>(null);
        }
    }
}
