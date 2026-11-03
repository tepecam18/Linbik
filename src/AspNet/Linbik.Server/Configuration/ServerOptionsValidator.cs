using Linbik.Core;
using Microsoft.Extensions.Options;

namespace Linbik.Server.Configuration;

/// <summary>
/// Validates <see cref="ServerOptions"/> configuration at startup.
/// Ensures all required values are provided and valid before application starts.
/// </summary>
public sealed class ServerOptionsValidator(IOptions<Linbik.Core.Configuration.LinbikOptions>? coreOptions = null) : IValidateOptions<ServerOptions>
{
    public ValidateOptionsResult Validate(string? name, ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> errors = [];

        // PackageName is required when audience validation is enabled
        if (string.IsNullOrWhiteSpace(coreOptions?.Value.PackageName) &&
            coreOptions?.Value.KeylessMode != true)
        {
            errors.Add("Linbik:PackageName is required for incoming token audience validation.");
        }

        var publicKey = coreOptions?.Value.PublicKey;
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            if (coreOptions?.Value.KeylessMode != true)
                errors.Add("Linbik:PublicKey is required. Server validation uses the shared LinbikOptions key.");
        }
        else
        {
            try
            {
                if (Convert.FromBase64String(publicKey).Length != 32)
                    errors.Add("Linbik:PublicKey must be a Base64-encoded 32-byte Ed25519 public key.");
            }
            catch (FormatException)
            {
                errors.Add("Linbik:PublicKey must be valid Base64.");
            }
        }

        if (string.IsNullOrWhiteSpace(coreOptions?.Value.JwtIssuer))
            errors.Add("Linbik:JwtIssuer is required for incoming token validation.");
        // IntegrationEndpointPath validation
        if (string.IsNullOrWhiteSpace(options.IntegrationEndpointPath) || !options.IntegrationEndpointPath.StartsWith('/'))
        {
            errors.Add($"Linbik:Server:IntegrationEndpointPath must start with '/'. Current value: '{options.IntegrationEndpointPath}'.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
