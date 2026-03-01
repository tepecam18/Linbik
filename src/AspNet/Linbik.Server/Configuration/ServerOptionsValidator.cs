using Linbik.Core;
using Microsoft.Extensions.Options;

namespace Linbik.Server.Configuration;

/// <summary>
/// Validates <see cref="ServerOptions"/> configuration at startup.
/// Ensures all required values are provided and valid before application starts.
/// </summary>
public sealed class ServerOptionsValidator : IValidateOptions<ServerOptions>
{
    public ValidateOptionsResult Validate(string? name, ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> errors = [];

        // PackageName is required when audience validation is enabled
        if (options.ValidateAudience && string.IsNullOrWhiteSpace(options.PackageName))
        {
            errors.Add("Linbik:Server:PackageName is required when ValidateAudience is enabled. Set it to this service's package name.");
        }

        // JwtIssuer validation
        if (string.IsNullOrWhiteSpace(options.JwtIssuer))
        {
            errors.Add("Linbik:Server:JwtIssuer is required.");
        }

        // ClockSkew validation
        if (options.ClockSkewMinutes < 0 || options.ClockSkewMinutes > 60)
        {
            errors.Add($"Linbik:Server:ClockSkewMinutes must be between 0 and 60. Current value: {options.ClockSkewMinutes}.");
        }

        // Token lifetime validations
        if (options.AccessTokenExpiration < 1 || options.AccessTokenExpiration > 1440)
        {
            errors.Add($"Linbik:Server:AccessTokenExpiration must be between 1 and 1440 (24 hours). Current value: {options.AccessTokenExpiration}.");
        }

        if (options.RefreshTokenExpirationDays < 1 || options.RefreshTokenExpirationDays > 365)
        {
            errors.Add($"Linbik:Server:RefreshTokenExpirationDays must be between 1 and 365. Current value: {options.RefreshTokenExpirationDays}.");
        }

        if (options.AuthorizationCodeLifetimeMinutes < 1 || options.AuthorizationCodeLifetimeMinutes > 60)
        {
            errors.Add($"Linbik:Server:AuthorizationCodeLifetimeMinutes must be between 1 and 60. Current value: {options.AuthorizationCodeLifetimeMinutes}.");
        }

        // IntegrationEndpointPath validation
        if (!options.IntegrationEndpointPath.StartsWith('/'))
        {
            errors.Add($"Linbik:Server:IntegrationEndpointPath must start with '/'. Current value: '{options.IntegrationEndpointPath}'.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
