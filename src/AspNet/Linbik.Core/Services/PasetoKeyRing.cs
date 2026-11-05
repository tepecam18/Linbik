using Linbik.Core.Configuration;
using Linbik.Core.Models;
using Linbik.Core.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Linbik.Core.Services;

/// <summary>
/// In-memory PASETO anahtar halkası; thread-safe rotasyon desteği ile.
/// Signing tarafı (Linbik.Api) kullanır. Doğrulama tarafı (Linbik.Server)
/// yalnızca tek public key ile <see cref="IPasetoHelper"/> kullanır.
/// </summary>
public sealed class PasetoKeyRing : IPasetoKeyRing
{
    private readonly PasetoKeyRingOptions _opts;
    private readonly Dictionary<string, PasetoKey> _keys;
    private readonly object _lock = new();
    private string _activeKeyId;

    public PasetoKeyRing(IOptions<PasetoKeyRingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _opts = options.Value;
        if (_opts.Keys is null || _opts.Keys.Any(k => k is null || string.IsNullOrWhiteSpace(k.KeyId)))
            throw new InvalidOperationException("Linbik:PasetoKeyRing:Keys must be an array of key objects with non-empty KeyId values. Do not include private key material in error reports.");
        if (_opts.Keys.Select(k => k.KeyId).Distinct(StringComparer.Ordinal).Count() != _opts.Keys.Count)
            throw new InvalidOperationException("Linbik:PasetoKeyRing:Keys contains duplicate KeyId values. Give every key a unique KeyId and set ActiveKeyId to the signing key's ID.");
        if (_opts.OverlapWindow < TimeSpan.Zero || _opts.OverlapWindow > DateTime.MaxValue - DateTime.UtcNow)
            throw new InvalidOperationException("Linbik:PasetoKeyRing:OverlapWindow must be non-negative and fit within the supported DateTime range. Example: 24:00:00.");
        _keys = _opts.Keys.ToDictionary(k => k.KeyId, StringComparer.Ordinal);
        _activeKeyId = _opts.ActiveKeyId;
    }

    /// <inheritdoc/>
    public PasetoKey Active
    {
        get
        {
            lock (_lock)
            {
                if (_keys.TryGetValue(_activeKeyId, out var key))
                    return key;

                throw new InvalidOperationException(
                    $"Active PASETO key '{_activeKeyId}' not found in key ring. " +
                    "Set 'Linbik:PasetoKeyRing:ActiveKeyId' to an existing KeyId in 'Linbik:PasetoKeyRing:Keys', or rotate a key before signing.");
            }
        }
    }

    /// <inheritdoc/>
    public bool TryGet(string keyId, out PasetoKey key)
    {
        lock (_lock)
        {
            if (_keys.TryGetValue(keyId, out var k) && !k.IsExpired)
            {
                key = k;
                return true;
            }

            key = default!;
            return false;
        }
    }

    /// <inheritdoc/>
    public Task RotateAsync(PasetoKey newKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(newKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(newKey.KeyId);

        lock (_lock)
        {
            // Expire the current active key after OverlapWindow to allow in-flight tokens
            if (_keys.TryGetValue(_activeKeyId, out var current) && current.NotAfter is null)
            {
                current.NotAfter = DateTime.UtcNow.Add(_opts.OverlapWindow);
            }

            _keys[newKey.KeyId] = newKey;
            _activeKeyId = newKey.KeyId;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PasetoKey> GetValid()
    {
        lock (_lock)
        {
            return _keys.Values
                .Where(k => !k.IsExpired)
                .ToList()
                .AsReadOnly();
        }
    }
}
