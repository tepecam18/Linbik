using Microsoft.Extensions.Options;

namespace Linbik.Core.Configuration;

/// <summary>
/// Validates <see cref="LinbikOptions"/> configuration at startup.
/// Ensures all required values are provided and valid before application starts.
/// </summary>
public sealed class LinbikOptionsValidator : IValidateOptions<LinbikOptions>
{
    /// <summary>
    /// Validates the Linbik options configuration.
    /// </summary>
    /// <param name="name">The name of the options instance being validated.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
    public ValidateOptionsResult Validate(string? name, LinbikOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> errors = [];

        // Validate LinbikUrl (always required, even in KeylessMode)
        if (string.IsNullOrWhiteSpace(options.LinbikUrl))
        {
            errors.Add("Linbik:LinbikUrl is required. Set the Linbik server base URL (e.g., 'https://linbik.com').");
        }
        else if (!Uri.TryCreate(options.LinbikUrl, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            errors.Add($"Linbik:LinbikUrl must be a valid HTTP/HTTPS URL. Current value: '{options.LinbikUrl}'.");
        }

        // In KeylessMode, skip ServiceId/ApiKey/ClientId validation
        // These will be provisioned automatically at runtime
        if (!options.KeylessMode)
        {
            ValidateStandardMode(options, errors);
        }

        // Validate token lifetimes (always validated)
        if (options.AuthorizationCodeLifetimeMinutes < 1 || options.AuthorizationCodeLifetimeMinutes > 60)
        {
            errors.Add($"Linbik:AuthorizationCodeLifetimeMinutes must be between 1 and 60. Current value: {options.AuthorizationCodeLifetimeMinutes}.");
        }

        if (options.AccessTokenLifetimeMinutes < 1 || options.AccessTokenLifetimeMinutes > 1440)
        {
            errors.Add($"Linbik:AccessTokenLifetimeMinutes must be between 1 and 1440 (24 hours). Current value: {options.AccessTokenLifetimeMinutes}.");
        }

        if (options.RefreshTokenLifetimeDays < 1 || options.RefreshTokenLifetimeDays > 365)
        {
            errors.Add($"Linbik:RefreshTokenLifetimeDays must be between 1 and 365. Current value: {options.RefreshTokenLifetimeDays}.");
        }

        // Validate endpoints (always validated)
        if (!options.AuthorizationEndpoint.StartsWith("/"))
        {
            errors.Add($"Linbik:AuthorizationEndpoint must start with '/'. Current value: '{options.AuthorizationEndpoint}'.");
        }

        if (!options.TokenEndpoint.StartsWith("/"))
        {
            errors.Add($"Linbik:TokenEndpoint must start with '/'. Current value: '{options.TokenEndpoint}'.");
        }

        if (!options.RefreshEndpoint.StartsWith("/"))
        {
            errors.Add($"Linbik:RefreshEndpoint must start with '/'. Current value: '{options.RefreshEndpoint}'.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    /// <summary>
    /// Validates fields required in standard (non-keyless) mode.
    /// </summary>
    private static void ValidateStandardMode(LinbikOptions options, List<string> errors)
    {
        // Validate ServiceId
        if (string.IsNullOrWhiteSpace(options.ServiceId))
        {
            errors.Add("Linbik:ServiceId is required. Get this from Linbik service registration.");
        }
        else if (!Guid.TryParse(options.ServiceId, out _))
        {
            errors.Add($"Linbik:ServiceId must be a valid GUID. Current value: '{options.ServiceId}'.");
        }

        // Validate Clients
        if (options.Clients == null || options.Clients.Count == 0)
        {
            errors.Add("Linbik:Clients configuration is required. Define at least one Linbik client.");
        }
        else
        {
            foreach (var client in options.Clients)
            {
                if (string.IsNullOrWhiteSpace(client.ClientId))
                {
                    errors.Add($"Linbik client '{client.ClientId}': ClientId is required.");
                }
                else if (!Guid.TryParse(client.ClientId, out _))
                {
                    errors.Add($"Linbik client '{client.ClientId}': ClientId must be a valid GUID. Current value: '{client.ClientId}'.");
                }
                if (client.ClientType != LinbikClientType.Web && client.ClientType != LinbikClientType.Mobile)
                {
                    errors.Add($"Linbik client '{client.ClientId}': ClientType must be either 'Web' or 'Mobile'. Current value: '{client.ClientType}'.");
                }
            }
        }

        // Validate ApiKey
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add("Linbik:ApiKey is required. Get this from Linbik service registration.");
        }
        else if (options.ApiKey.Length < 32)
        {
            errors.Add("Linbik:ApiKey appears to be too short. Verify the API key from Linbik service registration.");
        }
    }
}
