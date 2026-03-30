using System.CommandLine;
using Linbik.CLI.Services;

namespace Linbik.CLI.Commands;

/// <summary>
/// linbik status — Shows current service status and connection info.
/// </summary>
internal static class StatusCommand
{
    public static Command Create()
    {
        var command = new Command("status", "Show current Linbik service status and connectivity");
        command.SetHandler(HandleAsync);
        return command;
    }

    private static async Task HandleAsync()
    {
        ConsoleUI.Header("Linbik Status");
        Console.WriteLine();

        var basePath = Directory.GetCurrentDirectory();

        // Load credentials
        var credentials = await CredentialsManager.LoadAsync(basePath);
        if (credentials == null)
        {
            ConsoleUI.Error("Linbik konfigürasyonu bulunamadı.");
            ConsoleUI.Info("Önce 'linbik init' komutunu çalıştırın.");
            return;
        }

        // Local credentials info
        ConsoleUI.Step("Yerel Konfigürasyon:");
        ConsoleUI.Info($"  ServiceId:   {credentials.ServiceId}");
        ConsoleUI.Info($"  ClientId:    {credentials.ClientId}");
        ConsoleUI.Info($"  Claimed:     {(credentials.IsClaimed ? "✓ Evet" : "✗ Hayır")}");
        ConsoleUI.Info($"  Provisioned: {credentials.ProvisionedAt:yyyy-MM-dd HH:mm:ss UTC}");

        if (credentials.ExpiresAt != default)
        {
            var remaining = credentials.ExpiresAt - DateTime.UtcNow;
            if (remaining.TotalSeconds > 0)
                ConsoleUI.Info($"  Expires:     {credentials.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC} ({remaining.TotalHours:F0}h kaldı)");
            else
                ConsoleUI.Warning($"  Expires:     SÜRESİ DOLMUŞ ({credentials.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC})");
        }

        Console.WriteLine();

        // Check appsettings.json
        var appSettingsPath = AppSettingsManager.FindAppSettings(basePath);
        if (appSettingsPath != null)
        {
            var config = await AppSettingsManager.ReadConfigAsync(appSettingsPath);
            if (config != null)
            {
                ConsoleUI.Step("appsettings.json:");
                ConsoleUI.Info($"  LinbikUrl:   {config.LinbikUrl}");
                ConsoleUI.Info($"  ServiceId:   {config.ServiceId}");
                ConsoleUI.Info($"  KeylessMode: {config.KeylessMode}");

                // Check if credentials match appsettings
                if (config.ServiceId != credentials.ServiceId)
                {
                    ConsoleUI.Warning("  ⚠ ServiceId eşleşmiyor! 'linbik export-config' çalıştırın.");
                }
            }
            else
            {
                ConsoleUI.Warning($"appsettings.json'da Linbik konfigürasyonu yok: {appSettingsPath}");
            }
        }
        else
        {
            ConsoleUI.Warning("appsettings.json bulunamadı.");
        }

        Console.WriteLine();

        // Try to check remote status
        var linbikUrl = "https://linbik.com";
        if (appSettingsPath != null)
        {
            var config = await AppSettingsManager.ReadConfigAsync(appSettingsPath);
            if (config?.LinbikUrl != null)
                linbikUrl = config.LinbikUrl;
        }

        ConsoleUI.Step("Sunucu Durumu:");
        try
        {
            using var apiClient = new LinbikApiClient(linbikUrl);
            var status = await apiClient.GetServiceStatusAsync(credentials.ServiceId, credentials.ApiKey);

            if (status != null)
            {
                ConsoleUI.Success($"  Bağlantı:    ✓ OK ({linbikUrl})");
                ConsoleUI.Info($"  Service:     {status.Name}");
                ConsoleUI.Info($"  Provisioned: {(status.IsProvisioned ? "✓ Evet" : "✗ Hayır")}");

                if (status.Clients is { Count: > 0 })
                {
                    ConsoleUI.Info($"  Clients:     {status.Clients.Count} adet");
                    foreach (var client in status.Clients)
                    {
                        ConsoleUI.Info($"    - {client.Name}: {client.RedirectUri} ({(client.IsActive ? "aktif" : "pasif")})");
                    }
                }
            }
            else
            {
                ConsoleUI.Warning($"  Bağlantı:    ✗ Yanıt alınamadı ({linbikUrl})");
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.Error($"  Bağlantı:    ✗ Hata — {ex.Message}");
        }

        Console.WriteLine();
    }
}
