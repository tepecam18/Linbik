namespace Linbik.CLI.Services;

/// <summary>
/// Diagnoses which Linbik components are present in Program.cs.
/// </summary>
internal record ProgramCsDiagnosis
{
    public bool HasAddLinbik { get; init; }
    public bool HasAddLinbikJwtAuth { get; init; }
    public bool HasEnsureLinbik { get; init; }
    public bool HasUseLinbikJwtAuth { get; init; }
    public bool HasUseAuthentication { get; init; }
    public bool HasUseAuthorization { get; init; }
    public bool HasUseRouting { get; init; }
    public bool HasMapControllers { get; init; }

    /// <summary>
    /// All required Linbik components are correctly wired up.
    /// </summary>
    public bool IsFullyConfigured =>
        HasAddLinbik && HasEnsureLinbik
        && (HasAddLinbikJwtAuth == HasUseLinbikJwtAuth);

    /// <summary>
    /// No Linbik markers exist at all — a fresh project.
    /// </summary>
    public bool HasNoLinbikIntegration =>
        !HasAddLinbik && !HasAddLinbikJwtAuth
        && !HasEnsureLinbik && !HasUseLinbikJwtAuth;
}

/// <summary>
/// Captures what was fixed and what needs manual attention.
/// </summary>
internal class ProgramCsFixResult
{
    public List<string> Fixes { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool Modified { get; set; }
}

/// <summary>
/// Detects and modifies Program.cs to inject Linbik service registration and middleware.
/// Handles both fresh injection and partial configuration repair.
/// </summary>
internal static class ProgramCsManager
{
    /// <summary>
    /// Find Program.cs in the given directory.
    /// </summary>
    public static string? FindProgramCs(string basePath)
    {
        var candidate = Path.Combine(basePath, "Program.cs");
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Analyze Program.cs content to detect which Linbik components are present.
    /// Uses "MethodName(" pattern to distinguish AddLinbik( from AddLinbikJwtAuth( etc.
    /// </summary>
    public static ProgramCsDiagnosis Diagnose(string content)
    {
        return new ProgramCsDiagnosis
        {
            HasAddLinbik = content.Contains("AddLinbik(", StringComparison.Ordinal),
            HasAddLinbikJwtAuth = content.Contains("AddLinbikJwtAuth(", StringComparison.Ordinal),
            HasEnsureLinbik = content.Contains("EnsureLinbik(", StringComparison.Ordinal),
            HasUseLinbikJwtAuth = content.Contains("UseLinbikJwtAuth(", StringComparison.Ordinal),
            HasUseAuthentication = content.Contains("UseAuthentication(", StringComparison.Ordinal),
            HasUseAuthorization = content.Contains("UseAuthorization(", StringComparison.Ordinal),
            HasUseRouting = content.Contains("UseRouting(", StringComparison.Ordinal),
            HasMapControllers = content.Contains("MapControllers(", StringComparison.Ordinal),
        };
    }

    /// <summary>
    /// Check whether Program.cs already contains complete Linbik integration.
    /// </summary>
    public static bool HasLinbikIntegration(string content) =>
        Diagnose(content).IsFullyConfigured;

    /// <summary>
    /// Inject or fix Linbik service registrations and middleware in Program.cs.
    /// Auto-fixes clearly broken configurations and warns about potentially intentional gaps.
    /// </summary>
    public static async Task<ProgramCsFixResult> InjectLinbikAsync(string programCsPath)
    {
        var content = await File.ReadAllTextAsync(programCsPath);
        var diagnosis = Diagnose(content);
        var result = new ProgramCsFixResult();

        if (diagnosis.IsFullyConfigured)
            return result;

        var lines = new List<string>(content.Split('\n').Select(l => l.TrimEnd('\r')));

        // ── Phase 1: Fix service registrations ──────────────────────
        FixServiceRegistrations(lines, diagnosis, result);

        // Re-diagnose after service fixes (line indices shifted)
        var midContent = string.Join("\n", lines);
        var midDiagnosis = Diagnose(midContent);

        // ── Phase 2: Fix middleware pipeline ────────────────────────
        FixMiddlewarePipeline(lines, midDiagnosis, result);

        // ── Phase 3: Generate warnings for ambiguous gaps ───────────
        var finalContent = string.Join("\n", lines);
        var finalDiagnosis = Diagnose(finalContent);
        GenerateWarnings(finalDiagnosis, result);

        if (result.Modified)
        {
            var output = string.Join(Environment.NewLine, lines);
            await File.WriteAllTextAsync(programCsPath, output);
        }

        return result;
    }

    // ─── Service Registration Fixes ─────────────────────────────────

    private static void FixServiceRegistrations(
        List<string> lines, ProgramCsDiagnosis diagnosis, ProgramCsFixResult result)
    {
        if (diagnosis.HasAddLinbik && diagnosis.HasAddLinbikJwtAuth)
            return; // Both present — nothing to fix

        var builderIdx = FindLine(lines, l =>
            l.Contains("WebApplication.CreateBuilder", StringComparison.Ordinal));

        if (builderIdx < 0)
            return; // Can't safely inject without builder pattern

        if (diagnosis.HasNoLinbikIntegration)
        {
            // Fresh injection → add full service chain
            var insertIdx = builderIdx + 1;
            while (insertIdx < lines.Count && string.IsNullOrWhiteSpace(lines[insertIdx]))
                insertIdx++;

            lines.InsertRange(insertIdx, new[]
            {
                "",
                "builder.Services",
                "    .AddLinbik()",
                "    .AddLinbikJwtAuth();",
                ""
            });

            result.Fixes.Add("AddLinbik() ve AddLinbikJwtAuth() servis kayıtları eklendi.");
            result.Modified = true;
            return;
        }

        // ── Fix: Missing AddLinbik (prerequisite for all Linbik services) ──
        if (!diagnosis.HasAddLinbik)
        {
            if (diagnosis.HasAddLinbikJwtAuth)
            {
                // AddLinbikJwtAuth exists without AddLinbik → insert before it
                var jwtIdx = FindLine(lines, l =>
                    l.Contains("AddLinbikJwtAuth(", StringComparison.Ordinal));

                if (jwtIdx >= 0)
                {
                    var trimmed = lines[jwtIdx].TrimStart();
                    if (trimmed.StartsWith(".AddLinbikJwtAuth", StringComparison.Ordinal))
                    {
                        // Chain continuation → insert .AddLinbik() before it
                        var indent = GetIndent(lines[jwtIdx]);
                        lines.Insert(jwtIdx, $"{indent}.AddLinbik()");
                    }
                    else
                    {
                        // Standalone / builder.Services.AddLinbikJwtAuth → add before
                        lines.Insert(jwtIdx, "builder.Services.AddLinbik();");
                    }

                    result.Fixes.Add("Eksik AddLinbik() eklendi (AddLinbikJwtAuth ön koşulu).");
                    result.Modified = true;
                }
            }
            else
            {
                // Only middleware exists without any service registrations
                var insertIdx = builderIdx + 1;
                while (insertIdx < lines.Count && string.IsNullOrWhiteSpace(lines[insertIdx]))
                    insertIdx++;

                var serviceLines = new List<string> { "" };
                serviceLines.Add("builder.Services");
                serviceLines.Add("    .AddLinbik()");

                if (diagnosis.HasUseLinbikJwtAuth)
                {
                    serviceLines.Add("    .AddLinbikJwtAuth();");
                    result.Fixes.Add("Eksik AddLinbik() ve AddLinbikJwtAuth() servis kayıtları eklendi.");
                }
                else
                {
                    // Close the chain with AddLinbik only
                    serviceLines[^1] += ";";
                    result.Fixes.Add("Eksik AddLinbik() servis kaydı eklendi.");
                }

                serviceLines.Add("");
                lines.InsertRange(insertIdx, serviceLines);
                result.Modified = true;
            }
        }

        // ── Fix: Missing AddLinbikJwtAuth (required by UseLinbikJwtAuth) ──
        if (diagnosis.HasAddLinbik && !diagnosis.HasAddLinbikJwtAuth && diagnosis.HasUseLinbikJwtAuth)
        {
            if (InsertIntoServiceChain(lines, "AddLinbik(", "AddLinbikJwtAuth()"))
            {
                result.Fixes.Add("Eksik AddLinbikJwtAuth() eklendi (UseLinbikJwtAuth middleware'i için gerekli).");
                result.Modified = true;
            }
        }
    }

    // ─── Middleware Pipeline Fixes ───────────────────────────────────

    private static void FixMiddlewarePipeline(
        List<string> lines, ProgramCsDiagnosis diagnosis, ProgramCsFixResult result)
    {
        var appBuildIdx = FindLine(lines, l =>
            l.Contains(".Build()", StringComparison.Ordinal)
            && (l.Contains("var app", StringComparison.Ordinal)
                || l.Contains("WebApplication", StringComparison.Ordinal)
                || l.TrimStart().StartsWith("app", StringComparison.Ordinal)));

        if (appBuildIdx < 0)
            return;

        // Middleware fully configured for what's registered
        if (diagnosis.HasEnsureLinbik && (diagnosis.HasUseLinbikJwtAuth || !diagnosis.HasAddLinbikJwtAuth))
            return;

        if (!diagnosis.HasEnsureLinbik && !diagnosis.HasUseLinbikJwtAuth && diagnosis.HasAddLinbik)
        {
            // No Linbik middleware at all but services exist → fresh middleware injection
            var insertIdx = appBuildIdx + 1;
            while (insertIdx < lines.Count && string.IsNullOrWhiteSpace(lines[insertIdx]))
                insertIdx++;

            var middlewareLines = new List<string> { "" };

            if (diagnosis.HasMapControllers && !diagnosis.HasUseRouting)
                middlewareLines.Add("app.UseRouting();");

            if (!diagnosis.HasUseAuthentication)
                middlewareLines.Add("app.UseAuthentication();");

            if (!diagnosis.HasUseAuthorization)
                middlewareLines.Add("app.UseAuthorization();");

            middlewareLines.Add("app.EnsureLinbik();");

            if (diagnosis.HasAddLinbikJwtAuth)
                middlewareLines.Add("app.UseLinbikJwtAuth();");

            middlewareLines.Add("");
            lines.InsertRange(insertIdx, middlewareLines);

            result.Fixes.Add("Middleware pipeline'a EnsureLinbik()"
                + (diagnosis.HasAddLinbikJwtAuth ? " ve UseLinbikJwtAuth()" : "") + " eklendi.");
            result.Modified = true;
            return;
        }

        // ── Partial: Add missing EnsureLinbik ──
        if (!diagnosis.HasEnsureLinbik && diagnosis.HasAddLinbik)
        {
            var insertIdx = appBuildIdx + 1;
            while (insertIdx < lines.Count && string.IsNullOrWhiteSpace(lines[insertIdx]))
                insertIdx++;

            lines.Insert(insertIdx, "app.EnsureLinbik();");
            result.Fixes.Add("Eksik EnsureLinbik() middleware'i eklendi.");
            result.Modified = true;
        }
    }

    // ─── Warning Generation ─────────────────────────────────────────

    private static void GenerateWarnings(ProgramCsDiagnosis diagnosis, ProgramCsFixResult result)
    {
        if (diagnosis.HasAddLinbik && !diagnosis.HasAddLinbikJwtAuth && !diagnosis.HasUseLinbikJwtAuth)
        {
            result.Warnings.Add(
                "AddLinbikJwtAuth() bulunamadı. JWT kimlik doğrulama gerekiyorsa builder zincirine .AddLinbikJwtAuth() ekleyin.");
        }

        if (diagnosis.HasAddLinbikJwtAuth && !diagnosis.HasUseLinbikJwtAuth)
        {
            result.Warnings.Add(
                "UseLinbikJwtAuth() middleware'i bulunamadı. JWT endpoint'leri (login, callback, refresh, logout) aktif olmayacak.");
        }

        if ((diagnosis.HasUseLinbikJwtAuth || diagnosis.HasEnsureLinbik) && !diagnosis.HasUseAuthentication)
        {
            result.Warnings.Add(
                "UseAuthentication() bulunamadı. Authentication middleware olmadan JWT koruması çalışmaz.");
        }

        if ((diagnosis.HasUseLinbikJwtAuth || diagnosis.HasEnsureLinbik) && !diagnosis.HasUseAuthorization)
        {
            result.Warnings.Add(
                "UseAuthorization() bulunamadı. Yetkilendirme gerektiren endpoint'ler korumasız kalabilir.");
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Insert a new chained method call after an existing marker in a fluent service chain.
    /// </summary>
    private static bool InsertIntoServiceChain(List<string> lines, string existingMarker, string newCall)
    {
        var markerIdx = FindLine(lines, l =>
            l.Contains(existingMarker, StringComparison.Ordinal));

        if (markerIdx < 0)
            return false;

        // Find the end of the method chain (line ending with ;)
        var chainEndIdx = markerIdx;
        while (chainEndIdx < lines.Count && !lines[chainEndIdx].TrimEnd().EndsWith(";"))
            chainEndIdx++;

        if (chainEndIdx >= lines.Count)
            return false;

        var endLine = lines[chainEndIdx];

        // Determine indent for new chained call
        string newIndent;
        if (chainEndIdx == markerIdx)
        {
            // Single-line → indent + 4 spaces for continuation
            newIndent = GetIndent(endLine) + "    ";
        }
        else
        {
            // Multi-line chain → match indent of chain end line
            newIndent = GetIndent(endLine);
        }

        // Remove trailing ; and insert chain continuation on next line
        lines[chainEndIdx] = endLine.TrimEnd().TrimEnd(';');
        lines.Insert(chainEndIdx + 1, $"{newIndent}.{newCall};");

        return true;
    }

    private static string GetIndent(string line)
    {
        var trimmed = line.TrimStart();
        return line[..(line.Length - trimmed.Length)];
    }

    private static int FindLine(List<string> lines, Func<string, bool> predicate)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (predicate(lines[i]))
                return i;
        }
        return -1;
    }
}
