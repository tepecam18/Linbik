namespace Linbik.CLI.Services;

/// <summary>
/// Linbik authentication provider choice for Program.cs injection.
/// </summary>
internal enum LinbikAuthType
{
    Jwt,
    Paseto
}

/// <summary>
/// Diagnoses which Linbik components are present in Program.cs.
/// Tracks both JWT and PASETO so the same Program.cs can be analyzed for either.
/// </summary>
internal record ProgramCsDiagnosis
{
    public bool HasAddLinbik { get; init; }
    public bool HasAddLinbikJwtAuth { get; init; }
    public bool HasAddLinbikPasetoAuth { get; init; }
    public bool HasEnsureLinbik { get; init; }
    public bool HasUseLinbikJwtAuth { get; init; }
    public bool HasUseLinbikPasetoAuth { get; init; }
    public bool HasUseAuthentication { get; init; }
    public bool HasUseAuthorization { get; init; }
    public bool HasUseRouting { get; init; }
    public bool HasMapControllers { get; init; }

    public bool HasAnyAuthRegistration => HasAddLinbikJwtAuth || HasAddLinbikPasetoAuth;
    public bool HasAnyAuthMiddleware => HasUseLinbikJwtAuth || HasUseLinbikPasetoAuth;

    /// <summary>
    /// All required Linbik components are correctly wired up
    /// (each registered auth provider also has its middleware).
    /// </summary>
    public bool IsFullyConfigured =>
        HasAddLinbik && HasEnsureLinbik
        && HasAddLinbikJwtAuth == HasUseLinbikJwtAuth
        && HasAddLinbikPasetoAuth == HasUseLinbikPasetoAuth;

    /// <summary>
    /// No Linbik markers exist at all — a fresh project.
    /// </summary>
    public bool HasNoLinbikIntegration =>
        !HasAddLinbik && !HasAnyAuthRegistration
        && !HasEnsureLinbik && !HasAnyAuthMiddleware;
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
            HasAddLinbikPasetoAuth = content.Contains("AddLinbikPasetoAuth(", StringComparison.Ordinal),
            HasEnsureLinbik = content.Contains("EnsureLinbik(", StringComparison.Ordinal),
            HasUseLinbikJwtAuth = content.Contains("UseLinbikJwtAuth(", StringComparison.Ordinal),
            HasUseLinbikPasetoAuth = content.Contains("UseLinbikPasetoAuth(", StringComparison.Ordinal),
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
    /// <param name="programCsPath">Path to Program.cs.</param>
    /// <param name="authType">Which Linbik auth provider to wire up (JWT or PASETO).</param>
    public static async Task<ProgramCsFixResult> InjectLinbikAsync(
        string programCsPath,
        LinbikAuthType authType = LinbikAuthType.Jwt)
    {
        var content = await File.ReadAllTextAsync(programCsPath);
        var diagnosis = Diagnose(content);
        var result = new ProgramCsFixResult();

        if (diagnosis.IsFullyConfigured)
            return result;

        var lines = new List<string>(content.Split('\n').Select(l => l.TrimEnd('\r')));
        var spec = AuthSpec.For(authType);

        // ── Phase 1: Fix service registrations ──────────────────────
        FixServiceRegistrations(lines, diagnosis, result, spec);

        // Re-diagnose after service fixes (line indices shifted)
        var midContent = string.Join("\n", lines);
        var midDiagnosis = Diagnose(midContent);

        // ── Phase 2: Fix middleware pipeline ────────────────────────
        FixMiddlewarePipeline(lines, midDiagnosis, result, spec);

        // ── Phase 3: Generate warnings for ambiguous gaps ───────────
        var finalContent = string.Join("\n", lines);
        var finalDiagnosis = Diagnose(finalContent);
        GenerateWarnings(finalDiagnosis, result, spec);

        if (result.Modified)
        {
            var output = string.Join(Environment.NewLine, lines);
            await File.WriteAllTextAsync(programCsPath, output);
        }

        return result;
    }

    // ─── Auth provider method spec ──────────────────────────────────

    private sealed record AuthSpec(
        string AddMethod,
        string UseMethod,
        Func<ProgramCsDiagnosis, bool> HasAdd,
        Func<ProgramCsDiagnosis, bool> HasUse)
    {
        public static AuthSpec For(LinbikAuthType type) => type switch
        {
            LinbikAuthType.Paseto => new AuthSpec(
                "AddLinbikPasetoAuth",
                "UseLinbikPasetoAuth",
                d => d.HasAddLinbikPasetoAuth,
                d => d.HasUseLinbikPasetoAuth),
            _ => new AuthSpec(
                "AddLinbikJwtAuth",
                "UseLinbikJwtAuth",
                d => d.HasAddLinbikJwtAuth,
                d => d.HasUseLinbikJwtAuth),
        };
    }

    // ─── Service Registration Fixes ─────────────────────────────────

    private static void FixServiceRegistrations(
        List<string> lines, ProgramCsDiagnosis diagnosis, ProgramCsFixResult result, AuthSpec spec)
    {
        var hasAuthAdd = spec.HasAdd(diagnosis);
        var hasAuthUse = spec.HasUse(diagnosis);

        if (diagnosis.HasAddLinbik && hasAuthAdd)
            return; // Both present — nothing to fix for this auth provider

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
                $"    .{spec.AddMethod}();",
                ""
            });

            result.Fixes.Add($"AddLinbik() ve {spec.AddMethod}() servis kayıtları eklendi.");
            result.Modified = true;
            return;
        }

        // ── Fix: Missing AddLinbik (prerequisite for all Linbik services) ──
        if (!diagnosis.HasAddLinbik)
        {
            if (hasAuthAdd)
            {
                // Auth registration exists without AddLinbik → insert before it
                var authIdx = FindLine(lines, l =>
                    l.Contains($"{spec.AddMethod}(", StringComparison.Ordinal));

                if (authIdx >= 0)
                {
                    var trimmed = lines[authIdx].TrimStart();
                    if (trimmed.StartsWith($".{spec.AddMethod}", StringComparison.Ordinal))
                    {
                        // Chain continuation → insert .AddLinbik() before it
                        var indent = GetIndent(lines[authIdx]);
                        lines.Insert(authIdx, $"{indent}.AddLinbik()");
                    }
                    else
                    {
                        // Standalone / builder.Services.AddLinbik...Auth → add before
                        lines.Insert(authIdx, "builder.Services.AddLinbik();");
                    }

                    result.Fixes.Add($"Eksik AddLinbik() eklendi ({spec.AddMethod} ön koşulu).");
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

                if (hasAuthUse)
                {
                    serviceLines.Add($"    .{spec.AddMethod}();");
                    result.Fixes.Add($"Eksik AddLinbik() ve {spec.AddMethod}() servis kayıtları eklendi.");
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

        // ── Fix: Missing auth registration (required by its middleware) ──
        if (diagnosis.HasAddLinbik && !hasAuthAdd && hasAuthUse)
        {
            if (InsertIntoServiceChain(lines, "AddLinbik(", $"{spec.AddMethod}()"))
            {
                result.Fixes.Add($"Eksik {spec.AddMethod}() eklendi ({spec.UseMethod} middleware'i için gerekli).");
                result.Modified = true;
            }
        }
    }

    // ─── Middleware Pipeline Fixes ───────────────────────────────────

    private static void FixMiddlewarePipeline(
        List<string> lines, ProgramCsDiagnosis diagnosis, ProgramCsFixResult result, AuthSpec spec)
    {
        var hasAuthAdd = spec.HasAdd(diagnosis);
        var hasAuthUse = spec.HasUse(diagnosis);

        var appBuildIdx = FindLine(lines, l =>
            l.Contains(".Build()", StringComparison.Ordinal)
            && (l.Contains("var app", StringComparison.Ordinal)
                || l.Contains("WebApplication", StringComparison.Ordinal)
                || l.TrimStart().StartsWith("app", StringComparison.Ordinal)));

        if (appBuildIdx < 0)
            return;

        // Middleware fully configured for what's registered
        if (diagnosis.HasEnsureLinbik && (hasAuthUse || !hasAuthAdd))
            return;

        if (!diagnosis.HasEnsureLinbik && !hasAuthUse && diagnosis.HasAddLinbik)
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

            if (hasAuthAdd)
                middlewareLines.Add($"app.{spec.UseMethod}();");

            middlewareLines.Add("");
            lines.InsertRange(insertIdx, middlewareLines);

            result.Fixes.Add("Middleware pipeline'a EnsureLinbik()"
                + (hasAuthAdd ? $" ve {spec.UseMethod}()" : "") + " eklendi.");
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

    private static void GenerateWarnings(ProgramCsDiagnosis diagnosis, ProgramCsFixResult result, AuthSpec spec)
    {
        var hasAuthAdd = spec.HasAdd(diagnosis);
        var hasAuthUse = spec.HasUse(diagnosis);

        if (diagnosis.HasAddLinbik && !hasAuthAdd && !hasAuthUse)
        {
            result.Warnings.Add(
                $"{spec.AddMethod}() bulunamadı. Kimlik doğrulama gerekiyorsa builder zincirine .{spec.AddMethod}() ekleyin.");
        }

        if (hasAuthAdd && !hasAuthUse)
        {
            result.Warnings.Add(
                $"{spec.UseMethod}() middleware'i bulunamadı. Auth endpoint'leri (login, callback, refresh, logout) aktif olmayacak.");
        }

        if ((hasAuthUse || diagnosis.HasEnsureLinbik) && !diagnosis.HasUseAuthentication)
        {
            result.Warnings.Add(
                "UseAuthentication() bulunamadı. Authentication middleware olmadan token koruması çalışmaz.");
        }

        if ((hasAuthUse || diagnosis.HasEnsureLinbik) && !diagnosis.HasUseAuthorization)
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
