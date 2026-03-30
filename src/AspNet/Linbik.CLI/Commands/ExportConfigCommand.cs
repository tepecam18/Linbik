using System.CommandLine;
using Linbik.CLI.Services;

namespace Linbik.CLI.Commands;

/// <summary>
/// linbik export-config — Exports stored credentials to appsettings.json.
/// </summary>
internal static class ExportConfigCommand
{
    public static Command Create()
    {
        var pathOption = new Option<string?>(
            "--path",
            "Path to appsettings.json (default: auto-detect)");

        var command = new Command("export-config", "Export Linbik credentials to appsettings.json")
        {
            pathOption
        };

        command.SetHandler(HandleAsync, pathOption);
        return command;
    }

    private static async Task HandleAsync(string? targetPath)
    {
        ConsoleUI.Header("Linbik Export Config");
        Console.WriteLine();

        var basePath = Directory.GetCurrentDirectory();

        // Load credentials
        var credentials = await CredentialsManager.LoadAsync(basePath);
        if (credentials == null)
        {
            ConsoleUI.Error("Linbik kimlik bilgileri bulunamadı.");
            ConsoleUI.Info("Önce 'linbik init' komutunu çalıştırın.");
            return;
        }

        ConsoleUI.Info($"ServiceId: {credentials.ServiceId}");
        ConsoleUI.Info($"ClientId:  {credentials.ClientId}");
        ConsoleUI.Info($"Claimed:   {(credentials.IsClaimed ? "Evet" : "Hayır")}");
        Console.WriteLine();

        // Determine target file
        var appSettingsPath = targetPath ?? AppSettingsManager.FindAppSettings(basePath);
        if (appSettingsPath == null)
        {
            ConsoleUI.Error("appsettings.json bulunamadı.");
            ConsoleUI.Info("--path parametresi ile dosya yolunu belirtin.");
            return;
        }

        ConsoleUI.Step($"Yazılıyor: {appSettingsPath}");

        // Read existing config to preserve Linbik URL
        var existingConfig = await AppSettingsManager.ReadConfigAsync(appSettingsPath);
        var linbikUrl = existingConfig?.LinbikUrl ?? "https://linbik.com";

        await AppSettingsManager.WriteConfigAsync(
            appSettingsPath,
            linbikUrl,
            credentials.ServiceId,
            credentials.ClientId,
            credentials.ApiKey);

        ConsoleUI.Success("Konfigürasyon başarıyla yazıldı.");
        Console.WriteLine();

        ConsoleUI.Info("appsettings.json içeriği:");
        var config = await AppSettingsManager.ReadConfigAsync(appSettingsPath);
        if (config != null)
        {
            ConsoleUI.Info($"  LinbikUrl:  {config.LinbikUrl}");
            ConsoleUI.Info($"  ServiceId:  {config.ServiceId}");
            ConsoleUI.Info($"  KeylessMode: {config.KeylessMode}");
        }
    }
}
