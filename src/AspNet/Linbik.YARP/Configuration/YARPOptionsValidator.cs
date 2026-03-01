using Microsoft.Extensions.Options;

namespace Linbik.YARP.Configuration;

/// <summary>
/// Validates <see cref="YARPOptions"/> configuration at startup.
/// Ensures integration service routes are properly configured.
/// </summary>
public sealed class YARPOptionsValidator : IValidateOptions<YARPOptions>
{
    public ValidateOptionsResult Validate(string? name, YARPOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> errors = [];

        // S2S timeout validation
        if (options.S2STimeoutSeconds < 1 || options.S2STimeoutSeconds > 300)
        {
            errors.Add($"Linbik:YARP:S2STimeoutSeconds must be between 1 and 300. Current value: {options.S2STimeoutSeconds}.");
        }

        // Validate each integration service configuration
        foreach (var (key, service) in options.IntegrationServices)
        {
            if (string.IsNullOrWhiteSpace(service.SourcePath))
            {
                errors.Add($"Linbik:YARP:IntegrationServices:{key}:SourcePath is required.");
            }

            if (string.IsNullOrWhiteSpace(service.TargetBaseUrl))
            {
                errors.Add($"Linbik:YARP:IntegrationServices:{key}:TargetBaseUrl is required.");
            }
            else if (!Uri.TryCreate(service.TargetBaseUrl, UriKind.Absolute, out var uri) ||
                     (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                errors.Add($"Linbik:YARP:IntegrationServices:{key}:TargetBaseUrl must be a valid HTTP/HTTPS URL. Current value: '{service.TargetBaseUrl}'.");
            }

            if (service.TimeoutSeconds < 1 || service.TimeoutSeconds > 300)
            {
                errors.Add($"Linbik:YARP:IntegrationServices:{key}:TimeoutSeconds must be between 1 and 300. Current value: {service.TimeoutSeconds}.");
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
