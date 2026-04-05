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
        ConsoleUI.Header(Messages.StatusHeader);
        Console.WriteLine();

        var basePath = Directory.GetCurrentDirectory();

        // Load credentials
        var credentials = await CredentialsManager.LoadAsync(basePath);
        if (credentials == null)
        {
            ConsoleUI.Error(Messages.ConfigNotFound);
            ConsoleUI.Info(Messages.RunInitFirst);
            return;
        }

        // Local credentials info
        ConsoleUI.Step(Messages.LocalConfig);
        ConsoleUI.Info($"  ServiceId:   {credentials.ServiceId}");
        ConsoleUI.Info($"  ClientId:    {credentials.ClientId}");
        ConsoleUI.Info($"  Claimed:     {(credentials.IsClaimed ? Messages.Provisioned : Messages.NotProvisioned)}");
        ConsoleUI.Info($"  Provisioned: {credentials.ProvisionedAt:yyyy-MM-dd HH:mm:ss UTC}");

        if (credentials.ExpiresAt != default)
        {
            var remaining = credentials.ExpiresAt - DateTime.UtcNow;
            if (remaining.TotalSeconds > 0)
                ConsoleUI.Info($"  Expires:     {credentials.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC} ({Messages.Remaining(remaining.TotalHours)})");
            else
                ConsoleUI.Warning($"  Expires:     {Messages.Expired} ({credentials.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC})");
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
                    ConsoleUI.Warning($"  {Messages.ServiceIdMismatch}");
                }
            }
            else
            {
                ConsoleUI.Warning(Messages.NoLinbikConfigInAppSettings(appSettingsPath));
            }
        }
        else
        {
            ConsoleUI.Warning(Messages.AppSettingsNotFoundShort);
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

        ConsoleUI.Step(Messages.ServerStatus);
        try
        {
            using var apiClient = new LinbikApiClient(linbikUrl);
            var status = await apiClient.GetServiceStatusAsync(credentials.ServiceId, credentials.ApiKey);

            if (status != null)
            {
                ConsoleUI.Success($"  {Messages.ConnectionOk(linbikUrl)}");
                ConsoleUI.Info($"  Service:     {status.Name}");
                ConsoleUI.Info($"  Provisioned: {(status.IsProvisioned ? Messages.Provisioned : Messages.NotProvisioned)}");

                if (status.Clients is { Count: > 0 })
                {
                    ConsoleUI.Info($"  Clients:     {Messages.ClientCount(status.Clients.Count)}");
                    foreach (var client in status.Clients)
                    {
                        ConsoleUI.Info($"    - {client.Name}: {client.RedirectUri} ({(client.IsActive ? Messages.Active : Messages.Inactive)})");
                    }
                }
            }
            else
            {
                ConsoleUI.Warning($"  {Messages.ConnectionNoResponse(linbikUrl)}");
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.Error($"  {Messages.ConnectionError(ex.Message)}");
        }

        Console.WriteLine();
    }
}
