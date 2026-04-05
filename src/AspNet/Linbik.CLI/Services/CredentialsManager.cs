using System.Text.Json;
using System.Text.Json.Serialization;

namespace Linbik.CLI.Services;

/// <summary>
/// Manages .linbik/credentials.json for persisting service credentials.
/// Uses the same format as Linbik.Core's FileLinbikCredentialStore.
/// </summary>
internal static class CredentialsManager
{
    private const string CredentialDir = ".linbik";
    private const string CredentialFile = "credentials.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    /// <summary>
    /// Load credentials from .linbik/credentials.json in the given directory.
    /// </summary>
    public static async Task<CliCredentials?> LoadAsync(string basePath)
    {
        var filePath = Path.Combine(basePath, CredentialDir, CredentialFile);
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            File.Delete(filePath);
            return null;
        }

        CliCredentials? creds;
        try
        {
            creds = JsonSerializer.Deserialize<CliCredentials>(json, JsonOptions);
        }
        catch (JsonException)
        {
            File.Delete(filePath);
            return null;
        }

        // Check if expired and unclaimed
        if (creds != null && creds.ExpiresAt < DateTime.UtcNow && !creds.IsClaimed)
        {
            // Expired unclaimed credentials — delete
            File.Delete(filePath);
            return null;
        }

        return creds;
    }

    /// <summary>
    /// Save credentials to .linbik/credentials.json in the given directory.
    /// Creates the .linbik directory if it doesn't exist.
    /// </summary>
    public static async Task SaveAsync(string basePath, CliCredentials credentials)
    {
        var dirPath = Path.Combine(basePath, CredentialDir);
        Directory.CreateDirectory(dirPath);

        var filePath = Path.Combine(dirPath, CredentialFile);
        var json = JsonSerializer.Serialize(credentials, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);

        // Ensure .linbik is in .gitignore
        EnsureGitIgnore(basePath);
    }

    /// <summary>
    /// Delete credentials file.
    /// </summary>
    public static void Delete(string basePath)
    {
        var filePath = Path.Combine(basePath, CredentialDir, CredentialFile);
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

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

/// <summary>
/// Credential data stored in .linbik/credentials.json.
/// Compatible with Linbik.Core's LinbikCredentials format.
/// </summary>
internal sealed class CliCredentials
{
    public string ServiceId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? ClaimToken { get; set; }
    public string ClaimUrl { get; set; } = string.Empty;
    public bool IsClaimed { get; set; }
    public DateTime ProvisionedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
