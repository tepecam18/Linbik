using System.Security.Cryptography;
using System.Text;
using Linbik.Core.Models;
using Linbik.Core.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

#if LINBIK_JWT
namespace Linbik.JwtAuthManager.Services;
#else
namespace Linbik.PasetoAuthManager.Services;
#endif

/// <summary>Immutable local session data. Stores hashes only, never raw refresh tokens.</summary>
public sealed record LinbikRefreshSession(Guid UserId, string Username, string? DisplayName, DateTimeOffset ExpiresAt);

/// <summary>Application-replaceable storage for local refresh tokens.</summary>
public interface ILinbikRefreshTokenStore
{
    Task CreateAsync(string tokenHash, LinbikRefreshSession session, CancellationToken cancellationToken = default);
    /// <summary>Atomically consume the current hash and install its replacement. Reject expired or revoked sessions.
    /// Reuse of a consumed hash must revoke its entire session, including the current replacement.</summary>
    Task<LinbikRefreshSession?> RotateAsync(string tokenHash, string replacementHash, CancellationToken cancellationToken = default);
    /// <summary>Revoke the session, including all rotated tokens, identified by this hash.</summary>
    Task RevokeAsync(string tokenHash, CancellationToken cancellationToken = default);
}

/// <summary>Process-local store. Restart loses sessions; multi-server deployments must replace this store.</summary>
public sealed class InMemoryLinbikRefreshTokenStore : ILinbikRefreshTokenStore, IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions());
    private sealed class Entry(LinbikRefreshSession session, string hash)
    {
        public LinbikRefreshSession Session { get; } = session;
        public string CurrentHash { get; set; } = hash;
        public bool Revoked { get; set; }
    }

    public Task CreateAsync(string tokenHash, LinbikRefreshSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (session.ExpiresAt <= DateTimeOffset.UtcNow) throw new ArgumentOutOfRangeException(nameof(session));
        cache.Set(tokenHash, new Entry(session, tokenHash), session.ExpiresAt);
        return Task.CompletedTask;
    }

    public Task<LinbikRefreshSession?> RotateAsync(string tokenHash, string replacementHash, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!cache.TryGetValue<Entry>(tokenHash, out var entry) || entry is null)
            return Task.FromResult<LinbikRefreshSession?>(null);
        lock (entry)
        {
            if (entry.Revoked || entry.Session.ExpiresAt <= DateTimeOffset.UtcNow)
                return Task.FromResult<LinbikRefreshSession?>(null);
            if (entry.CurrentHash != tokenHash)
            {
                entry.Revoked = true;
                return Task.FromResult<LinbikRefreshSession?>(null);
            }
            entry.CurrentHash = replacementHash;
            cache.Set(replacementHash, entry, entry.Session.ExpiresAt);
            return Task.FromResult<LinbikRefreshSession?>(entry.Session);
        }
    }

    public Task RevokeAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cache.TryGetValue<Entry>(tokenHash, out var entry) && entry is not null)
            lock (entry) entry.Revoked = true;
        return Task.CompletedTask;
    }

    public void Dispose() => cache.Dispose();
}

/// <summary>Selects upstream refresh or a local session without falling back after upstream failure.</summary>
public sealed class LinbikRefreshTokenManager(ILinbikRefreshTokenStore store)
{
#if LINBIK_JWT
    private const string Prefix = "linbik-local-jwt.";
#else
    private const string Prefix = "linbik-local-paseto.";
#endif
    private static string NewToken() => Prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task EnsureAsync(LinbikTokenResponse response, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(response.RefreshToken)) return;
        var expiry = new DateTimeOffset(expiresAt.ToUniversalTime());
        var token = NewToken();
        await store.CreateAsync(Hash(token), new(response.UserId, response.Username, response.DisplayName, expiry), cancellationToken);
        response.RefreshToken = token;
        response.RefreshTokenExpiresAt = expiry.ToUnixTimeSeconds();
    }

    public async Task<LinbikTokenResponse?> RefreshAsync(string token, ILinbikAuthClient client, CancellationToken cancellationToken = default)
    {
        if (!token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Never send another AuthManager's local token to Linbik.
            if (token.StartsWith("linbik-local-", StringComparison.Ordinal)) return null;
            var response = await client.RefreshTokensAsync(token, cancellationToken);
            if (response is null || response.UserId == Guid.Empty || string.IsNullOrWhiteSpace(response.Username)) return null;
            if (string.IsNullOrEmpty(response.RefreshToken)) response.RefreshToken = token;
            return response;
        }
        var replacement = NewToken();
        var session = await store.RotateAsync(Hash(token), Hash(replacement), cancellationToken);
        if (session is null) return null;
        return new LinbikTokenResponse
        {
            UserId = session.UserId, Username = session.Username, DisplayName = session.DisplayName ?? session.Username,
            RefreshToken = replacement, RefreshTokenExpiresAt = session.ExpiresAt.ToUnixTimeSeconds()
        };
    }

    public Task RevokeAsync(string? token, CancellationToken cancellationToken = default) =>
        token is not null && token.StartsWith(Prefix, StringComparison.Ordinal)
            ? store.RevokeAsync(Hash(token), cancellationToken) : Task.CompletedTask;
}
