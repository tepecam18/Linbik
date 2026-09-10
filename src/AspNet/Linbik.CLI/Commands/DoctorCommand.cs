using System.CommandLine;
using Linbik.CLI.Services;

namespace Linbik.CLI.Commands;

/// <summary>
/// linbik doctor — Read-only health check. Diagnoses Program.cs, appsettings.json,
/// and credentials without modifying anything. Reports what's missing per provider.
/// </summary>
internal static class DoctorCommand
{
    public static Command Create()
    {
        var command = new Command("doctor", "Diagnose Linbik integration state (read-only)");
        command.SetAction(async (_, _) =>
        {
            await HandleAsync();
            return 0;
        });
        return command;
    }

    private static async Task HandleAsync()
    {
        ConsoleUI.Header(Messages.DoctorHeader);
        Console.WriteLine();

        var basePath = Directory.GetCurrentDirectory();
        var issues = 0;

        // ── 1) Credentials ────────────────────────────────────────────
        ConsoleUI.Step(Messages.DoctorCredentialsSection);
        var credentials = await CredentialsManager.LoadAsync(basePath);
        if (credentials == null)
        {
            ConsoleUI.Warning("  .linbik/credentials.json " + Messages.DoctorNotFound);
            issues++;
        }
        else
        {
            ConsoleUI.Info($"  ServiceId: {credentials.ServiceId}");
            ConsoleUI.Info($"  Claimed:   {(credentials.IsClaimed ? Messages.Yes : Messages.No)}");
            if (!credentials.IsClaimed)
            {
                ConsoleUI.Warning("  " + Messages.DoctorUnclaimed);
                issues++;
            }
        }

        Console.WriteLine();

        // ── 2) appsettings.json ───────────────────────────────────────
        ConsoleUI.Step(Messages.DoctorAppSettingsSection);
        var appSettingsPath = AppSettingsManager.FindAppSettings(basePath);
        LinbikAppSettingsSnapshot? config = null;
        if (appSettingsPath == null)
        {
            ConsoleUI.Warning("  " + Messages.AppSettingsNotFoundShort);
            issues++;
        }
        else
        {
            config = await AppSettingsManager.ReadConfigAsync(appSettingsPath);
            ConsoleUI.Info($"  Path: {appSettingsPath}");
            if (config == null)
            {
                ConsoleUI.Warning("  " + Messages.NoLinbikConfigInAppSettings(appSettingsPath));
                issues++;
            }
            else
            {
                ConsoleUI.Info($"  LinbikUrl:  {config.Options.LinbikUrl}");
                ConsoleUI.Info($"  ServiceId:  {config.Options.ServiceId}");
                ConsoleUI.Info($"  JwtAuth:    {(config.HasJwtAuth ? Messages.Yes : Messages.No)}");
                ConsoleUI.Info($"  PasetoAuth: {(config.HasPasetoAuth ? Messages.Yes : Messages.No)}");

                if (string.IsNullOrEmpty(config.Options.ServiceId))
                {
                    ConsoleUI.Warning("  " + Messages.DoctorMissingServiceId);
                    issues++;
                }

                if (credentials != null && config.Options.ServiceId != credentials.ServiceId)
                {
                    ConsoleUI.Warning("  " + Messages.ServiceIdMismatch);
                    issues++;
                }

                if (!config.HasJwtAuth && !config.HasPasetoAuth)
                {
                    ConsoleUI.Warning("  " + Messages.DoctorNoAuthSection);
                    issues++;
                }
            }
        }

        Console.WriteLine();

        // ── 3) Program.cs ─────────────────────────────────────────────
        ConsoleUI.Step(Messages.DoctorProgramCsSection);
        var programCsPath = ProgramCsManager.FindProgramCs(basePath);
        if (programCsPath == null)
        {
            ConsoleUI.Warning("  " + Messages.ProgramCsNotFound);
            issues++;
        }
        else
        {
            ConsoleUI.Info($"  Path: {programCsPath}");
            var content = await File.ReadAllTextAsync(programCsPath);
            var diagnosis = ProgramCsManager.Diagnose(content);

            PrintCheck("AddLinbik()", diagnosis.HasAddLinbik, ref issues, required: true);
            PrintCheck("EnsureLinbik()", diagnosis.HasEnsureLinbik, ref issues, required: true);
            PrintCheck("AddLinbikJwtAuth()", diagnosis.HasAddLinbikJwtAuth, ref issues, required: false);
            PrintCheck("UseLinbikJwtAuth()", diagnosis.HasUseLinbikJwtAuth, ref issues,
                required: diagnosis.HasAddLinbikJwtAuth);
            PrintCheck("AddLinbikPasetoAuth()", diagnosis.HasAddLinbikPasetoAuth, ref issues, required: false);
            PrintCheck("UseLinbikPasetoAuth()", diagnosis.HasUseLinbikPasetoAuth, ref issues,
                required: diagnosis.HasAddLinbikPasetoAuth);
            PrintCheck("UseAuthentication()", diagnosis.HasUseAuthentication, ref issues,
                required: diagnosis.HasAnyAuthMiddleware || diagnosis.HasEnsureLinbik);
            PrintCheck("UseAuthorization()", diagnosis.HasUseAuthorization, ref issues,
                required: diagnosis.HasAnyAuthMiddleware || diagnosis.HasEnsureLinbik);

            // Cross-check: config says one provider, Program.cs uses another
            if (config != null)
            {
                if (config.HasJwtAuth && !diagnosis.HasAddLinbikJwtAuth)
                {
                    ConsoleUI.Warning("  " + Messages.DoctorConfigProgramMismatch("JwtAuth", "AddLinbikJwtAuth"));
                    issues++;
                }
                if (config.HasPasetoAuth && !diagnosis.HasAddLinbikPasetoAuth)
                {
                    ConsoleUI.Warning("  " + Messages.DoctorConfigProgramMismatch("PasetoAuth", "AddLinbikPasetoAuth"));
                    issues++;
                }
            }
        }

        Console.WriteLine();

        // ── Summary ───────────────────────────────────────────────────
        if (issues == 0)
            ConsoleUI.Success(Messages.DoctorAllGood);
        else
        {
            ConsoleUI.Warning(Messages.DoctorIssuesFound(issues));
            ConsoleUI.Info(Messages.DoctorRunInitHint);
        }

        Console.WriteLine();
    }

    private static void PrintCheck(string label, bool present, ref int issues, bool required)
    {
        if (present)
        {
            ConsoleUI.Success($"  {label}");
        }
        else if (required)
        {
            ConsoleUI.Error($"  {label} — {Messages.DoctorMissing}");
            issues++;
        }
        else
        {
            ConsoleUI.Info($"  {label} — {Messages.DoctorNotConfigured}");
        }
    }
}
