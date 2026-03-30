using System.Text.Json;
using System.Text.Json.Nodes;

namespace Linbik.CLI.Services;

/// <summary>
/// Reads and writes Linbik configuration to/from appsettings.json files.
/// </summary>
internal static class AppSettingsManager
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Find appsettings.json in the given directory or parent directories.
    /// </summary>
    public static string? FindAppSettings(string basePath)
    {
        var dir = new DirectoryInfo(basePath);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "appsettings.json");
            if (File.Exists(candidate))
                return candidate;

            // Also check appsettings.Development.json
            var devCandidate = Path.Combine(dir.FullName, "appsettings.Development.json");
            if (File.Exists(devCandidate))
                return devCandidate;

            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Write Linbik configuration to appsettings.json.
    /// Merges with existing content — does not overwrite other settings.
    /// </summary>
    public static async Task WriteConfigAsync(
        string filePath,
        string linbikUrl,
        string serviceId,
        string clientId,
        string apiKey,
        string? baseUrl = null,
        string? redirectUrl = null)
    {
        JsonNode root;

        if (File.Exists(filePath))
        {
            var existingJson = await File.ReadAllTextAsync(filePath);
            root = JsonNode.Parse(existingJson, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        // Build client config
        var clientConfig = new JsonObject
        {
            ["ClientId"] = clientId,
            ["BaseUrl"] = baseUrl ?? "https://localhost:5001",
            ["RedirectUrl"] = redirectUrl ?? "/auth/callback",
            ["ClientType"] = "Web"
        };

        // Build Linbik section
        var linbikSection = new JsonObject
        {
            ["LinbikUrl"] = linbikUrl,
            ["Name"] = "Web App",
            ["ServiceId"] = serviceId,
            ["ApiKey"] = apiKey,
            ["Clients"] = new JsonArray { clientConfig }
        };

        // Preserve existing JwtAuth section if present
        if (root["Linbik"] is JsonObject existingLinbik && existingLinbik["JwtAuth"] is JsonNode existingJwtAuth)
        {
            linbikSection["JwtAuth"] = existingJwtAuth.DeepClone();
        }

        root["Linbik"] = linbikSection;

        var outputJson = root.ToJsonString(WriteOptions);
        await File.WriteAllTextAsync(filePath, outputJson);
    }

    /// <summary>
    /// Read current Linbik configuration from appsettings.json.
    /// Returns null if section doesn't exist.
    /// </summary>
    public static async Task<LinbikConfig?> ReadConfigAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        var linbikNode = root?["Linbik"];
        if (linbikNode == null)
            return null;

        return new LinbikConfig
        {
            LinbikUrl = linbikNode["LinbikUrl"]?.GetValue<string>() ?? "",
            ServiceId = linbikNode["ServiceId"]?.GetValue<string>() ?? "",
            ApiKey = linbikNode["ApiKey"]?.GetValue<string>() ?? "",
            KeylessMode = linbikNode["KeylessMode"]?.GetValue<bool>() ?? false
        };
    }
}

internal sealed class LinbikConfig
{
    public string LinbikUrl { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool KeylessMode { get; set; }
}
