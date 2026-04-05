using System.CommandLine;
using System.Diagnostics;
using Linbik.CLI.Services;

namespace Linbik.CLI.Commands;

/// <summary>
/// linbik init — Interactive service setup with OAuth login.
/// Opens browser for Linbik authentication, provisions service, writes config.
/// </summary>
internal static class InitCommand
{
    private const string DefaultLinbikUrl = "https://api.linbik.com";

    public static Command Create()
    {
        var urlOption = new Option<string>(
            "--url",
            () => DefaultLinbikUrl,
            "Linbik server URL");

        var nameOption = new Option<string?>(
            "--name",
            "Application name (default: auto-detect from assembly/directory)");

        var command = new Command("init", "Initialize a new Linbik service and write configuration")
        {
            urlOption,
            nameOption
        };

        command.SetHandler(HandleAsync, urlOption, nameOption);
        return command;
    }

    private static async Task HandleAsync(string linbikUrl, string? appName)
    {
        ConsoleUI.Header(Messages.InitHeader);
        Console.WriteLine();

        var basePath = Directory.GetCurrentDirectory();

        // Check for existing credentials
        var existing = await CredentialsManager.LoadAsync(basePath);
        if (existing is { IsClaimed: true })
        {
            ConsoleUI.Warning(Messages.ExistingClaimedService);
            ConsoleUI.Info($"ServiceId: {existing.ServiceId}");
            ConsoleUI.Info($"ClientId:  {existing.ClientId}");

            if (!ConsoleUI.Confirm(Messages.ResetConfirm, defaultYes: false))
            {
                ConsoleUI.Info(Messages.Cancelled);
                return;
            }

            CredentialsManager.Delete(basePath);
        }

        // Check for existing appsettings.json Linbik configuration
        var appSettingsPath = AppSettingsManager.FindAppSettings(basePath);
        LinbikConfig? existingConfig = null;
        if (appSettingsPath != null)
        {
            existingConfig = await AppSettingsManager.ReadConfigAsync(appSettingsPath);
            if (existingConfig != null && !string.IsNullOrEmpty(existingConfig.ServiceId))
            {
                ConsoleUI.Warning(Messages.ExistingAppSettingsConfig);
                ConsoleUI.Info($"  LinbikUrl:  {existingConfig.LinbikUrl}");
                ConsoleUI.Info($"  ServiceId:  {existingConfig.ServiceId}");

                if (ConsoleUI.Confirm(Messages.UseExistingConfigConfirm))
                {
                    // Use existing config values — skip provisioning
                    linbikUrl = existingConfig.LinbikUrl;

                    // Still ensure Program.cs is configured
                    InjectProgramCs(basePath);

                    Console.WriteLine();
                    ConsoleUI.Header(Messages.SetupComplete);
                    ConsoleUI.Success(Messages.RunApp);
                    Console.WriteLine();
                    return;
                }
            }
        }

        // Auto-detect app name
        appName ??= DetectAppName(basePath);
        appName = ConsoleUI.Prompt(Messages.PromptServiceName, appName) ?? "MyApp";

        // Detect app URL
        var detectedUrl = DetectAppUrl(basePath);
        var appUrl = ConsoleUI.Prompt(Messages.PromptAppUrl, detectedUrl) ?? "https://localhost:5001";
        var callbackPath = ConsoleUI.Prompt(Messages.PromptCallbackPath, "/api/linbik/callback") ?? "/api/linbik/callback";

        Console.WriteLine();
        ConsoleUI.Step(Messages.StepCreatingService);

        // 1. Provision service
        using var apiClient = new LinbikApiClient(linbikUrl);
        var provision = await apiClient.ProvisionAsync(appName, appUrl, callbackPath);

        if (provision == null)
        {
            ConsoleUI.Error(Messages.ServiceCreateFailed);
            return;
        }

        ConsoleUI.Success(Messages.ServiceCreated(appName));
        ConsoleUI.Info($"ServiceId: {provision.ServiceId}");
        ConsoleUI.Info($"ClientId:  {provision.ClientId}");

        // Save credentials
        var credentials = new CliCredentials
        {
            ServiceId = provision.ServiceId.ToString(),
            ClientId = provision.ClientId.ToString(),
            ApiKey = provision.ApiKey,
            ClaimToken = provision.ClaimToken,
            IsClaimed = false,
            ProvisionedAt = DateTime.UtcNow,
            ExpiresAt = provision.ExpiresAt
        };
        await CredentialsManager.SaveAsync(basePath, credentials);

        // 2. OAuth login to claim the service
        Console.WriteLine();
        ConsoleUI.Step(Messages.StepLoginPrompt);

        var claimedApiKey = await PerformOAuthLogin(apiClient, linbikUrl, provision, appUrl, callbackPath);

        if (claimedApiKey != null)
        {
            credentials.ApiKey = claimedApiKey;
            credentials.IsClaimed = true;
            credentials.ClaimToken = null;
            await CredentialsManager.SaveAsync(basePath, credentials);

            ConsoleUI.Success(Messages.ServiceClaimedSuccess);
        }

        // 3. Write appsettings.json
        Console.WriteLine();
        appSettingsPath ??= AppSettingsManager.FindAppSettings(basePath);
        if (appSettingsPath != null)
        {
            if (ConsoleUI.Confirm(Messages.UpdateAppSettingsConfirm(Path.GetFileName(appSettingsPath))))
            {
                await AppSettingsManager.WriteConfigAsync(
                    appSettingsPath,
                    linbikUrl,
                    credentials.ServiceId,
                    credentials.ClientId,
                    credentials.ApiKey,
                    appUrl,
                    callbackPath);

                ConsoleUI.Success(Messages.ConfigWritten(appSettingsPath));
            }
        }
        else
        {
            ConsoleUI.Warning(Messages.AppSettingsNotFound);
        }

        // 4. Update Program.cs
        Console.WriteLine();
        InjectProgramCs(basePath);

        // Summary
        Console.WriteLine();
        ConsoleUI.Header(Messages.SetupComplete);
        ConsoleUI.Success(Messages.RunApp);
        Console.WriteLine();
    }

    private static void InjectProgramCs(string basePath)
    {
        var programCsPath = ProgramCsManager.FindProgramCs(basePath);
        if (programCsPath == null)
        {
            ConsoleUI.Warning(Messages.ProgramCsNotFound);
            return;
        }

        var content = File.ReadAllText(programCsPath);
        var diagnosis = ProgramCsManager.Diagnose(content);

        if (diagnosis.IsFullyConfigured)
        {
            ConsoleUI.Info(Messages.ProgramCsAlreadyConfigured);
            return;
        }

        if (!diagnosis.HasNoLinbikIntegration)
            ConsoleUI.Warning("Program.cs kısmen yapılandırılmış, eksikler tespit edildi.");

        if (!ConsoleUI.Confirm(Messages.UpdateProgramCsConfirm))
            return;

        try
        {
            var result = ProgramCsManager.InjectLinbikAsync(programCsPath).GetAwaiter().GetResult();

            foreach (var fix in result.Fixes)
                ConsoleUI.Success(fix);

            foreach (var warning in result.Warnings)
                ConsoleUI.Warning(warning);

            if (result.Modified)
                ConsoleUI.Success(Messages.ProgramCsUpdated);
            else if (result.Warnings.Count > 0)
                ConsoleUI.Info("Dosya değiştirilmedi. Yukarıdaki uyarıları kontrol edin.");
        }
        catch (Exception ex)
        {
            ConsoleUI.Error(Messages.ProgramCsUpdateFailed(ex.Message));
        }
    }

    private static async Task<string?> PerformOAuthLogin(
        LinbikApiClient apiClient,
        string linbikUrl,
        ProvisionResponse provision,
        string appUrl,
        string callbackPath)
    {
        try
        {
            // Start temporary HTTP listener for callback
            var (listener, port) = LinbikApiClient.StartCallbackListener();
            var callbackUrl = $"http://localhost:{port}/";

            ConsoleUI.Info(Messages.BrowserOpening(provision.ClaimUrl));

            // Open browser
            try
            {
                Process.Start(new ProcessStartInfo(provision.ClaimUrl) { UseShellExecute = true });
            }
            catch
            {
                ConsoleUI.Warning(Messages.BrowserOpenFailed);
                Console.WriteLine($"  {provision.ClaimUrl}");
            }

            ConsoleUI.Info(Messages.ClaimInstructions);
            ConsoleUI.Info(Messages.ClaimServiceBind);
            ConsoleUI.Info(Messages.ApiKeyPromptInstruction);
            Console.Write("Api key: ");
            var NewApiKey = Console.ReadLine();

            if (string.IsNullOrEmpty(NewApiKey))
            {
                ConsoleUI.Error(Messages.ApiKeyEmpty);
                return null;
            }

            return NewApiKey;
        }
        catch (OperationCanceledException)
        {
            ConsoleUI.Warning(Messages.LoginTimeout);
            return null;
        }
        catch (Exception ex)
        {
            ConsoleUI.Error(Messages.OAuthLoginError(ex.Message));
            return null;
        }
    }

    private static string DetectAppName(string basePath)
    {
        // Try to find .csproj file
        var csprojFiles = Directory.GetFiles(basePath, "*.csproj");
        if (csprojFiles.Length > 0)
        {
            return Path.GetFileNameWithoutExtension(csprojFiles[0]);
        }

        // Try package.json
        var packageJsonPath = Path.Combine(basePath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            try
            {
                var json = File.ReadAllText(packageJsonPath);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("name", out var nameElement))
                {
                    return nameElement.GetString() ?? Path.GetFileName(basePath);
                }
            }
            catch { /* ignore parse errors */ }
        }

        // Fallback to directory name
        return Path.GetFileName(basePath);
    }

    private static string DetectAppUrl(string basePath)
    {
        // Try to read from launchSettings.json
        var launchSettingsPath = Path.Combine(basePath, "Properties", "launchSettings.json");
        if (File.Exists(launchSettingsPath))
        {
            try
            {
                var json = File.ReadAllText(launchSettingsPath);
                var doc = System.Text.Json.JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("profiles", out var profiles))
                {
                    foreach (var profile in profiles.EnumerateObject())
                    {
                        if (profile.Value.TryGetProperty("applicationUrl", out var urlElement))
                        {
                            var urls = urlElement.GetString();
                            if (!string.IsNullOrEmpty(urls))
                            {
                                // Prefer HTTPS URL
                                var urlList = urls.Split(';', StringSplitOptions.RemoveEmptyEntries);
                                var httpsUrl = urlList.FirstOrDefault(u => u.StartsWith("https://"));
                                return httpsUrl ?? urlList[0];
                            }
                        }
                    }
                }
            }
            catch { /* ignore parse errors */ }
        }

        return "https://localhost:5001";
    }
}
