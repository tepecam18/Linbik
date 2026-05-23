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
                    "Configure 'Linbik:PasetoKeyRing:Keys' in appsettings.json.");
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
