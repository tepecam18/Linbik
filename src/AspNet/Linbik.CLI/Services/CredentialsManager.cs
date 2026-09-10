using Linbik.Core.Services;
using Linbik.Core.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Linbik.CLI.Services;

/// <summary>
/// Thin static wrapper around Linbik.Core's <see cref="FileLinbikCredentialStore"/> for
/// use from CLI commands, which have no DI container to resolve it through. Also owns
/// the .gitignore bookkeeping for .linbik/, which is CLI-specific and not Core's concern.
/// </summary>
internal static class CredentialsManager
{
    private const string CredentialDir = ".linbik";
    private const string CredentialFile = "credentials.json";

    private static readonly ILinbikCredentialStore Store =
        new FileLinbikCredentialStore(NullLogger<FileLinbikCredentialStore>.Instance);

    /// <summary>
    /// Load credentials from .linbik/credentials.json in the given directory.
    /// </summary>
    public static Task<LinbikCredentials?> LoadAsync(string basePath) => Store.LoadAsync();

    /// <summary>
    /// Save credentials to .linbik/credentials.json in the given directory.
    /// Creates the .linbik directory if it doesn't exist.
    /// </summary>
    public static async Task SaveAsync(string basePath, LinbikCredentials credentials)
    {
        await Store.SaveAsync(credentials);

        // Ensure .linbik is in .gitignore
        EnsureGitIgnore(basePath);
    }

    /// <summary>
    /// Delete credentials file.
    /// </summary>
    public static Task DeleteAsync(string basePath) => Store.DeleteAsync();

    /// <summary>
    /// Get the full path to the credentials file.
    /// </summary>
    public static string GetFilePath(string basePath)
        => Path.Combine(basePath, CredentialDir, CredentialFile);

    /// <summary>
    /// Ensure .linbik/ is in .gitignore.
    /// </summary>
    private static void EnsureGitIgnore(string basePath)
    {
        var gitignorePath = Path.Combine(basePath, ".gitignore");
        const string entry = ".linbik/";

        if (File.Exists(gitignorePath))
        {
            var content = File.ReadAllText(gitignorePath);
            if (content.Contains(entry, StringComparison.OrdinalIgnoreCase))
                return;

            File.AppendAllText(gitignorePath, $"{Environment.NewLine}# Linbik CLI credentials{Environment.NewLine}{entry}{Environment.NewLine}");
        }
        else
        {
            File.WriteAllText(gitignorePath, $"# Linbik CLI credentials{Environment.NewLine}{entry}{Environment.NewLine}");
        }
    }
}
