using System.Text.Json.Nodes;
using Linbik.CLI.Services;
using Linbik.Core.Configuration;

namespace Linbik.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "linbik-tests-" + Guid.NewGuid());

    [Fact]
    public async Task ExportPreservesUnrelatedSettingsAndOtherClients()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "appsettings.json");
        await File.WriteAllTextAsync(path, """
            { // appsettings supports comments and trailing commas
              "Logging": { "LogLevel": "Warning" },
              "Linbik": {
                "Name": "My service", "CookieDomain": "example.com",
                "YARP": { "Enabled": true }, "JwtAuth": { "PkceEnabled": true },
                "Clients": [
                  { "ClientId": "mobile", "Name": "Mobile", "ActionResultType": "Json", "RedirectUrl": "/existing" },
                  { "ClientId": "web", "Name": "Web" },
                ]
              }
            }
            """);
        await AppSettingsManager.WriteConfigAsync(path, "https://example.com", "service", "mobile", "test-key");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        Assert.Equal("Warning", root["Logging"]!["LogLevel"]!.GetValue<string>());
        Assert.Equal("My service", root["Linbik"]!["Name"]!.GetValue<string>());
        Assert.Equal("example.com", root["Linbik"]!["CookieDomain"]!.GetValue<string>());
        Assert.True(root["Linbik"]!["YARP"]!["Enabled"]!.GetValue<bool>());
        var config = await AppSettingsManager.ReadConfigAsync(path);
        Assert.NotNull(config);
        Assert.True(config.HasJwtAuth);
        Assert.Equal(2, config.Options.Clients.Count);
        Assert.Equal("/existing", config.Options.Clients[0].RedirectUrl);
        Assert.Equal(ActionResultType.Json, config.Options.Clients[0].ActionResultType);
    }

    [Fact]
    public async Task MissingFileCanBeCreatedAndRead()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "appsettings.json");
        Assert.Null(await AppSettingsManager.ReadConfigAsync(path));
        await AppSettingsManager.WriteConfigAsync(path, "https://example.com", "service", "client", "test-key");
        Assert.Equal("service", (await AppSettingsManager.ReadConfigAsync(path))!.Options.ServiceId);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
