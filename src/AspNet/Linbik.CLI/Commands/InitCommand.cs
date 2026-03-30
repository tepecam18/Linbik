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
    private const string DefaultLinbikUrl = "https://linbik.com";

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
        ConsoleUI.Header("Linbik Init — Servis Kurulumu");
        Console.WriteLine();

        var basePath = Directory.GetCurrentDirectory();

        // Check for existing credentials
        var existing = await CredentialsManager.LoadAsync(basePath);
        if (existing is { IsClaimed: true })
        {
            ConsoleUI.Warning("Bu dizinde zaten claimed bir Linbik servisi mevcut.");
            ConsoleUI.Info($"ServiceId: {existing.ServiceId}");
            ConsoleUI.Info($"ClientId:  {existing.ClientId}");

            if (!ConsoleUI.Confirm("Mevcut konfigürasyonu sıfırlayıp yeniden başlamak istiyor musunuz?", defaultYes: false))
            {
                ConsoleUI.Info("İptal edildi.");
                return;
            }

            CredentialsManager.Delete(basePath);
        }

        // Auto-detect app name
        appName ??= DetectAppName(basePath);
        appName = ConsoleUI.Prompt("Servis adı", appName) ?? "MyApp";

        // Detect app URL
        var detectedUrl = DetectAppUrl(basePath);
        var appUrl = ConsoleUI.Prompt("Uygulama URL'i", detectedUrl) ?? "https://localhost:5001";
        var callbackPath = ConsoleUI.Prompt("Callback path", "/auth/callback") ?? "/auth/callback";

        Console.WriteLine();
        ConsoleUI.Step("Servis oluşturuluyor...");

        // 1. Provision service
        using var apiClient = new LinbikApiClient(linbikUrl);
        var provision = await apiClient.ProvisionAsync(appName, appUrl, callbackPath);

        if (provision == null)
        {
            ConsoleUI.Error("Servis oluşturulamadı. Lütfen tekrar deneyin.");
            return;
        }

        ConsoleUI.Success($"Servis oluşturuldu: {appName}");
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
        ConsoleUI.Step("Linbik hesabınızla giriş yapın...");

        var claimedApiKey = await PerformOAuthLogin(apiClient, linbikUrl, provision, appUrl, callbackPath);

        if (claimedApiKey != null)
        {
            credentials.ApiKey = claimedApiKey;
            credentials.IsClaimed = true;
            credentials.ClaimToken = null;
            await CredentialsManager.SaveAsync(basePath, credentials);

            ConsoleUI.Success("Servis başarıyla hesabınıza bağlandı!");
        }
        else
        {
            ConsoleUI.Warning("Servis henüz hesapla ilişkilendirilmedi. Daha sonra claim edebilirsiniz:");
            ConsoleUI.Info($"Claim URL: {provision.ClaimUrl}");
        }

        // 3. Write appsettings.json
        Console.WriteLine();
        var appSettingsPath = AppSettingsManager.FindAppSettings(basePath);
        if (appSettingsPath != null)
        {
            if (ConsoleUI.Confirm($"appsettings.json güncellenmeli mi? ({Path.GetFileName(appSettingsPath)})"))
            {
                await AppSettingsManager.WriteConfigAsync(
                    appSettingsPath,
                    linbikUrl,
                    credentials.ServiceId,
                    credentials.ClientId,
                    credentials.ApiKey,
                    appUrl,
                    callbackPath);

                ConsoleUI.Success($"Konfigürasyon yazıldı: {appSettingsPath}");
            }
        }
        else
        {
            ConsoleUI.Warning("appsettings.json bulunamadı. Konfigürasyonu manuel ekleyin veya 'linbik export-config' kullanın.");
        }

        // Summary
        Console.WriteLine();
        ConsoleUI.Header("Kurulum Tamamlandı");
        ConsoleUI.Success("Uygulamanızı başlatabilirsiniz: dotnet run");
        Console.WriteLine();
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

            // Build authorization URL — redirect to Linbik auth page
            var authUrl = $"{linbikUrl.TrimEnd('/')}/auth/{provision.ClientId}";

            ConsoleUI.Info($"Tarayıcı açılıyor: {authUrl}");

            // Open browser
            try
            {
                Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
            }
            catch
            {
                ConsoleUI.Warning("Tarayıcı otomatik açılamadı. Lütfen aşağıdaki URL'i tarayıcıda açın:");
                Console.WriteLine($"  {authUrl}");
            }

            ConsoleUI.Info("Giriş bekleniyor... (60 saniye timeout)");

            // Wait for callback with authorization code
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var code = await LinbikApiClient.WaitForCallbackAsync(listener, cts.Token);

            if (string.IsNullOrEmpty(code))
            {
                ConsoleUI.Error("Authorization code alınamadı.");
                return null;
            }

            ConsoleUI.Success("Giriş başarılı! Token exchange yapılıyor...");

            // Exchange code for tokens
            var tokenResponse = await apiClient.ExchangeCodeAsync(
                code,
                provision.ServiceId.ToString(),
                provision.ApiKey);

            if (tokenResponse == null)
            {
                ConsoleUI.Warning("Token exchange başarısız. Servis provisioned olarak kaldı.");
                return null;
            }

            ConsoleUI.Success($"Hoş geldiniz, {tokenResponse.DisplayName ?? tokenResponse.Username}!");

            // Check if claimed
            if (tokenResponse.Claimed == true && !string.IsNullOrEmpty(tokenResponse.NewApiKey))
            {
                return tokenResponse.NewApiKey;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            ConsoleUI.Warning("Giriş zaman aşımına uğradı. Servis provisioned olarak oluşturuldu.");
            return null;
        }
        catch (Exception ex)
        {
            ConsoleUI.Error($"OAuth login sırasında hata: {ex.Message}");
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
