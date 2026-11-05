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

        if (root["Linbik"] is { } section && section is not JsonObject)
            throw new InvalidOperationException($"'{filePath}': Linbik must be a JSON object, for example \"Linbik\": {{}}. No settings were written.");
        var linbikSection = root["Linbik"]?.DeepClone().AsObject() ?? new JsonObject();
        linbikSection["LinbikUrl"] = linbikUrl;
        linbikSection["Name"] ??= "Web App";
        linbikSection["ServiceId"] = serviceId;
        linbikSection["ApiKey"] = apiKey;

        if (linbikSection["Clients"] is { } configuredClients && configuredClients is not JsonArray)
            throw new InvalidOperationException($"'{filePath}': Linbik:Clients must be a JSON array, for example \"Clients\": [{{\"ClientId\":\"web\"}}]. No settings were written.");
        var clients = linbikSection["Clients"]?.AsArray() ?? new JsonArray();
        JsonObject? clientConfig = null;
        foreach (var (node, index) in clients.Select((node, index) => (node, index)))
        {
            if (node is not JsonObject client)
                throw new InvalidOperationException($"'{filePath}': Linbik:Clients:{index} must be a JSON object with a ClientId. No settings were written.");
            var configuredId = ReadString(client, "ClientId", $"{filePath}: Linbik:Clients:{index}");
            if (configuredId == clientId && clientConfig is null) clientConfig = client;
        }
        if (clientConfig is null)
        {
            clientConfig = new JsonObject { ["ClientId"] = clientId };
            clients.Add(clientConfig);
        }

        clientConfig["BaseUrl"] = baseUrl ?? ReadString(clientConfig, "BaseUrl", $"{filePath}: Linbik:Clients") ?? "https://localhost:5001";
        clientConfig["RedirectUrl"] = redirectUrl ?? ReadString(clientConfig, "RedirectUrl", $"{filePath}: Linbik:Clients") ?? "/api/linbik/callback";
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

        if (linbikNode is not JsonObject)
            throw new InvalidOperationException($"'{filePath}': Linbik must be a JSON object. Correct the section before reading its configuration.");
        var options = linbikNode.Deserialize<LinbikOptions>(ReadOptions) ?? new LinbikOptions();

        return new LinbikAppSettingsSnapshot
        {
            Options = options,
            HasJwtAuth = linbikNode["JwtAuth"] is JsonObject,
            HasPasetoAuth = linbikNode["PasetoAuth"] is JsonObject
        };
    }

    private static string? ReadString(JsonObject owner, string property, string path)
    {
        if (owner[property] is null) return null;
        if (owner[property] is JsonValue value && value.TryGetValue<string>(out var text)) return text;
        throw new InvalidOperationException($"'{path}:{property}' must be a JSON string. No settings were written.");
    }

    private static async Task<JsonObject?> ReadRootAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        if (JsonNode.Parse(json, documentOptions: DocumentOptions) is not JsonObject root)
            throw new InvalidOperationException($"'{filePath}' must contain a JSON object at the root, for example {{\"Linbik\": {{}}}}. No settings were written.");
        return root;
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
