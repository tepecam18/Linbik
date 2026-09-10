using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Linbik.Core.Configuration;

namespace Linbik.CLI.Services;

/// <summary>
/// Reads and writes Linbik configuration to/from appsettings.json files.
/// Binds directly to Linbik.Core's <see cref="LinbikOptions"/> so the CLI can't
/// silently drift from Core's actual config schema.
/// </summary>
internal static class AppSettingsManager
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
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
        var root = await ReadRootAsync(filePath) ?? new JsonObject();

        var linbikSection = root["Linbik"]?.DeepClone() as JsonObject ?? new JsonObject();
        linbikSection["LinbikUrl"] = linbikUrl;
        linbikSection["Name"] ??= "Web App";
        linbikSection["ServiceId"] = serviceId;
        linbikSection["ApiKey"] = apiKey;

        var clients = linbikSection["Clients"] as JsonArray ?? new JsonArray();
        var clientConfig = clients.OfType<JsonObject>()
            .FirstOrDefault(client => client["ClientId"]?.GetValue<string>() == clientId);
        if (clientConfig is null)
        {
            clientConfig = new JsonObject { ["ClientId"] = clientId };
            clients.Add(clientConfig);
        }

        clientConfig["BaseUrl"] = baseUrl ?? clientConfig["BaseUrl"]?.GetValue<string>() ?? "https://localhost:5001";
        clientConfig["RedirectUrl"] = redirectUrl ?? clientConfig["RedirectUrl"]?.GetValue<string>() ?? "/api/linbik/callback";
        clientConfig["ClientType"] ??= "Web";
        if (linbikSection["Clients"] is not JsonArray)
            linbikSection["Clients"] = clients;

        root["Linbik"] = linbikSection;

        var outputJson = root.ToJsonString(WriteOptions);
        await File.WriteAllTextAsync(filePath, outputJson);
    }

    /// <summary>
    /// Read the current Linbik configuration from appsettings.json, bound to Core's
    /// real <see cref="LinbikOptions"/>. Returns null if the "Linbik" section doesn't exist.
    /// </summary>
    public static async Task<LinbikAppSettingsSnapshot?> ReadConfigAsync(string filePath)
    {
        var root = await ReadRootAsync(filePath);
        var linbikNode = root?["Linbik"];
        if (linbikNode == null)
            return null;

        var options = linbikNode.Deserialize<LinbikOptions>(ReadOptions) ?? new LinbikOptions();

        return new LinbikAppSettingsSnapshot
        {
            Options = options,
            HasJwtAuth = linbikNode["JwtAuth"] is JsonObject,
            HasPasetoAuth = linbikNode["PasetoAuth"] is JsonObject
        };
    }

    private static async Task<JsonNode?> ReadRootAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonNode.Parse(json, documentOptions: DocumentOptions);
    }
}

/// <summary>
/// Bundles Core's real <see cref="LinbikOptions"/> with two CLI-only derived flags
/// (whether JwtAuth/PasetoAuth sub-sections are present) that aren't part of Core's schema.
/// </summary>
internal sealed class LinbikAppSettingsSnapshot
{
    public required LinbikOptions Options { get; init; }
    public bool HasJwtAuth { get; init; }
    public bool HasPasetoAuth { get; init; }
}
