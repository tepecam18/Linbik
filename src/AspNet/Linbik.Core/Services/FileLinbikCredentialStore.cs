using Linbik.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Linbik.Core.Services;

/// <summary>
/// File-based credential store for Keyless Mode.
/// Reads/writes .linbik/credentials.json with thread-safe access via SemaphoreSlim.
/// </summary>
public sealed class FileLinbikCredentialStore(ILogger<FileLinbikCredentialStore> logger) : ILinbikCredentialStore
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string GetCredentialsPath()
    {
        // Look for .linbik/ in the current working directory (project root)
        var dir = Path.Combine(Directory.GetCurrentDirectory(), ".linbik");
        return Path.Combine(dir, "credentials.json");
    }

    public async Task<LinbikCredentials?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var path = GetCredentialsPath();
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var credentials = JsonSerializer.Deserialize<LinbikCredentials>(json, _jsonOptions);

            if (credentials == null)
                return null;

            // Check if expired
            if (credentials.ExpiresAt < DateTime.UtcNow && !credentials.IsClaimed)
            {
                logger.LogInformation("Linbik credentials expired at {ExpiresAt}. Deleting cache.", credentials.ExpiresAt);
                File.Delete(path);
                return null;
            }

            return credentials;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load Linbik credentials from cache.");
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(LinbikCredentials credentials, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var path = GetCredentialsPath();
            var dir = Path.GetDirectoryName(path)!;

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(credentials, _jsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken);

            logger.LogDebug("Linbik credentials saved to {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save Linbik credentials to cache.");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var path = GetCredentialsPath();
            if (File.Exists(path))
            {
                File.Delete(path);
                logger.LogInformation("Linbik credentials cache deleted.");
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> HasValidCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await LoadAsync(cancellationToken);
        return credentials != null;
    }
}
