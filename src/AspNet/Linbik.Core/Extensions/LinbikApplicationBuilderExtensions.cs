using Linbik.Core.Builders.Interfaces;
using Linbik.Core.Configuration;
using Linbik.Core.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Linbik.Core.Extensions;

/// <summary>
/// Application builder extensions for Linbik startup validation.
/// </summary>
public static class LinbikApplicationBuilderExtensions
{
    /// <summary>
    /// Validates all registered Linbik modules at startup.
    /// Each <c>AddLinbik*()</c> call auto-registers its validator.
    /// This single call runs them all — no need to track which modules are active.
    /// <para>
    /// Place after <c>builder.Build()</c> and before <c>app.Run()</c>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.EnsureLinbik(); // Validates Core + JwtAuth + Server + YARP (whichever are registered)
    /// app.Run();
    /// </code>
    /// </example>
    public static IApplicationBuilder EnsureLinbik(this IApplicationBuilder app)
    {
        var validators = app.ApplicationServices
            .GetServices<ILinbikStartupValidator>()
            .OrderBy(v => v.Order);

        List<string> errors = [];

        foreach (var validator in validators)
        {
            try
            {
                validator.Validate(app.ApplicationServices);
            }
            catch (OptionsValidationException ex)
            {
                errors.AddRange(ex.Failures.Select(f => $"[{validator.ModuleName}] {f}"));
            }
            catch (Exception ex)
            {
                errors.Add($"[{validator.ModuleName}] {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Linbik startup validation failed:\n  • " +
                string.Join("\n  • ", errors));
        }

        // Auto-update RedirectUri is now handled by JwtAuthManager's startup validator
        // since AutoUpdateRedirectUri moved to JwtAuthOptions

        return app;
    }

}
