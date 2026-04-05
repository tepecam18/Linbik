using System.Globalization;

namespace Linbik.CLI.Services;

/// <summary>
/// Localized CLI messages. Returns Turkish if system culture is tr/tr-TR, English otherwise.
/// </summary>
internal static class Messages
{
    private static bool IsTurkish =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase);

    // ── Init Command ─────────────────────────────────────────────────
    public static string InitHeader => IsTurkish
        ? "Linbik Init — Servis Kurulumu"
        : "Linbik Init — Service Setup";

    public static string ExistingClaimedService => IsTurkish
        ? "Bu dizinde zaten claimed bir Linbik servisi mevcut."
        : "A claimed Linbik service already exists in this directory.";

    public static string ResetConfirm => IsTurkish
        ? "Mevcut konfigürasyonu sıfırlayıp yeniden başlamak istiyor musunuz?"
        : "Do you want to reset the existing configuration and start over?";

    public static string Cancelled => IsTurkish
        ? "İptal edildi."
        : "Cancelled.";

    public static string PromptServiceName => IsTurkish
        ? "Servis adı"
        : "Service name";

    public static string PromptAppUrl => IsTurkish
        ? "Uygulama URL'i"
        : "Application URL";

    public static string PromptCallbackPath => IsTurkish
        ? "Callback path"
        : "Callback path";

    public static string StepCreatingService => IsTurkish
        ? "Servis oluşturuluyor..."
        : "Creating service...";

    public static string ServiceCreateFailed => IsTurkish
        ? "Servis oluşturulamadı. Lütfen tekrar deneyin."
        : "Service creation failed. Please try again.";

    public static string ServiceCreated(string name) => IsTurkish
        ? $"Servis oluşturuldu: {name}"
        : $"Service created: {name}";

    public static string StepLoginPrompt => IsTurkish
        ? "Linbik hesabınızla giriş yapın..."
        : "Sign in with your Linbik account...";

    public static string BrowserOpening(string url) => IsTurkish
        ? $"Tarayıcı açılıyor: {url}"
        : $"Opening browser: {url}";

    public static string BrowserOpenFailed => IsTurkish
        ? "Tarayıcı otomatik açılamadı. Lütfen aşağıdaki URL'i tarayıcıda açın:"
        : "Could not open browser automatically. Please open the following URL in your browser:";

    public static string ClaimInstructions => IsTurkish
        ? "Giriş yapıldıktan sonra, tarayıcıda 'Claim Service' butonuna tıklayın."
        : "After signing in, click the 'Claim Service' button in the browser.";

    public static string ClaimServiceBind => IsTurkish
        ? "Bu işlem, servisinizin hesabınıza bağlanmasını sağlayacak."
        : "This will bind the service to your account.";

    public static string ApiKeyPromptInstruction => IsTurkish
        ? "Giriş yapıldıktan sonra, Api key'inizi göreceksiniz. Lütfen kopyalayın ve buraya yapıştırın."
        : "After signing in, you will see your API key. Please copy and paste it here.";

    public static string ApiKeyEmpty => IsTurkish
        ? "API key boş. Bu adım atlanıyor."
        : "API key is empty. Skipping this step.";

    public static string ServiceClaimedSuccess => IsTurkish
        ? "Servis başarıyla hesabınıza bağlandı!"
        : "Service successfully linked to your account!";

    public static string UpdateAppSettingsConfirm(string fileName) => IsTurkish
        ? $"appsettings.json güncellenmeli mi? ({fileName})"
        : $"Should appsettings.json be updated? ({fileName})";

    public static string ConfigWritten(string path) => IsTurkish
        ? $"Konfigürasyon yazıldı: {path}"
        : $"Configuration written: {path}";

    public static string AppSettingsNotFound => IsTurkish
        ? "appsettings.json bulunamadı. Konfigürasyonu manuel ekleyin veya 'linbik export-config' kullanın."
        : "appsettings.json not found. Add configuration manually or use 'linbik export-config'.";

    public static string SetupComplete => IsTurkish
        ? "Kurulum Tamamlandı"
        : "Setup Complete";

    public static string RunApp => IsTurkish
        ? "Uygulamanızı başlatabilirsiniz: dotnet run"
        : "You can start your application: dotnet run";

    public static string LoginTimeout => IsTurkish
        ? "Giriş zaman aşımına uğradı. Servis provisioned olarak oluşturuldu."
        : "Login timed out. Service was created in provisioned state.";

    public static string OAuthLoginError(string msg) => IsTurkish
        ? $"OAuth login sırasında hata: {msg}"
        : $"Error during OAuth login: {msg}";

    // ── AppSettings / Config detection ───────────────────────────────
    public static string ExistingAppSettingsConfig => IsTurkish
        ? "appsettings.json içinde mevcut Linbik konfigürasyonu bulundu."
        : "Existing Linbik configuration found in appsettings.json.";

    public static string UseExistingConfigConfirm => IsTurkish
        ? "Mevcut konfigürasyonu kullanmak istiyor musunuz?"
        : "Do you want to use the existing configuration?";

    // ── Program.cs modification ──────────────────────────────────────
    public static string ProgramCsNotFound => IsTurkish
        ? "Program.cs bulunamadı. Linbik entegrasyonunu manuel olarak ekleyin."
        : "Program.cs not found. Add Linbik integration manually.";

    public static string ProgramCsAlreadyConfigured => IsTurkish
        ? "Program.cs zaten Linbik entegrasyonunu içeriyor."
        : "Program.cs already contains Linbik integration.";

    public static string UpdateProgramCsConfirm => IsTurkish
        ? "Program.cs'ye Linbik entegrasyonu eklensin mi?"
        : "Should Linbik integration be added to Program.cs?";

    public static string ProgramCsUpdated => IsTurkish
        ? "Program.cs güncellendi — Linbik entegrasyonu eklendi."
        : "Program.cs updated — Linbik integration added.";

    public static string ProgramCsUpdateFailed(string msg) => IsTurkish
        ? $"Program.cs güncellenemedi: {msg}"
        : $"Failed to update Program.cs: {msg}";

    // ── Provision / API errors ───────────────────────────────────────
    public static string ProvisionFailed(string status, string body) => IsTurkish
        ? $"Provisioning başarısız ({status}): {body}"
        : $"Provision failed ({status}): {body}";

    public static string ServerInvalidResponse => IsTurkish
        ? "Sunucu geçersiz yanıt döndü (JSON bekleniyordu). URL'yi kontrol edin."
        : "Server returned an invalid response (expected JSON). Check the URL.";

    public static string HttpRequestError(string msg) => IsTurkish
        ? $"HTTP isteği sırasında hata: {msg}"
        : $"HTTP request error: {msg}";

    public static string JsonProcessingError(string msg) => IsTurkish
        ? $"JSON işleme hatası: {msg}"
        : $"JSON processing error: {msg}";

    public static string RequestTimeout => IsTurkish
        ? "İstek zaman aşımına uğradı."
        : "Request timed out.";

    public static string UnexpectedError(string msg) => IsTurkish
        ? $"Beklenmeyen hata: {msg}"
        : $"Unexpected error: {msg}";

    public static string TokenExchangeFailed(string status, string body) => IsTurkish
        ? $"Token exchange başarısız ({status}): {body}"
        : $"Token exchange failed ({status}): {body}";

    public static string StatusCheckFailed(string status, string body) => IsTurkish
        ? $"Durum kontrolü başarısız ({status}): {body}"
        : $"Status check failed ({status}): {body}";

    // ── Status Command ───────────────────────────────────────────────
    public static string StatusHeader => "Linbik Status";

    public static string ConfigNotFound => IsTurkish
        ? "Linbik konfigürasyonu bulunamadı."
        : "Linbik configuration not found.";

    public static string RunInitFirst => IsTurkish
        ? "Önce 'linbik init' komutunu çalıştırın."
        : "Run 'linbik init' first.";

    public static string LocalConfig => IsTurkish
        ? "Yerel Konfigürasyon:"
        : "Local Configuration:";

    public static string Yes => IsTurkish ? "Evet" : "Yes";
    public static string No => IsTurkish ? "Hayır" : "No";

    public static string Remaining(double hours) => IsTurkish
        ? $"{hours:F0}s kaldı"
        : $"{hours:F0}h remaining";

    public static string Expired => IsTurkish ? "SÜRESİ DOLMUŞ" : "EXPIRED";

    public static string ServiceIdMismatch => IsTurkish
        ? "⚠ ServiceId eşleşmiyor! 'linbik export-config' çalıştırın."
        : "⚠ ServiceId mismatch! Run 'linbik export-config'.";

    public static string NoLinbikConfigInAppSettings(string path) => IsTurkish
        ? $"appsettings.json'da Linbik konfigürasyonu yok: {path}"
        : $"No Linbik configuration in appsettings.json: {path}";

    public static string AppSettingsNotFoundShort => IsTurkish
        ? "appsettings.json bulunamadı."
        : "appsettings.json not found.";

    public static string ServerStatus => IsTurkish
        ? "Sunucu Durumu:"
        : "Server Status:";

    public static string ConnectionOk(string url) => IsTurkish
        ? $"Bağlantı:    ✓ OK ({url})"
        : $"Connection:  ✓ OK ({url})";

    public static string Provisioned => IsTurkish ? "✓ Evet" : "✓ Yes";
    public static string NotProvisioned => IsTurkish ? "✗ Hayır" : "✗ No";

    public static string ClientCount(int count) => IsTurkish
        ? $"{count} adet"
        : $"{count} client(s)";

    public static string Active => IsTurkish ? "aktif" : "active";
    public static string Inactive => IsTurkish ? "pasif" : "inactive";

    public static string ConnectionNoResponse(string url) => IsTurkish
        ? $"Bağlantı:    ✗ Yanıt alınamadı ({url})"
        : $"Connection:  ✗ No response ({url})";

    public static string ConnectionError(string msg) => IsTurkish
        ? $"Bağlantı:    ✗ Hata — {msg}"
        : $"Connection:  ✗ Error — {msg}";

    // ── Export Config Command ────────────────────────────────────────
    public static string ExportConfigHeader => "Linbik Export Config";

    public static string CredentialsNotFound => IsTurkish
        ? "Linbik kimlik bilgileri bulunamadı."
        : "Linbik credentials not found.";

    public static string WritingTo(string path) => IsTurkish
        ? $"Yazılıyor: {path}"
        : $"Writing to: {path}";

    public static string ConfigWrittenSuccess => IsTurkish
        ? "Konfigürasyon başarıyla yazıldı."
        : "Configuration written successfully.";

    public static string SpecifyPathOption => IsTurkish
        ? "--path parametresi ile dosya yolunu belirtin."
        : "Specify the file path with the --path option.";
}
