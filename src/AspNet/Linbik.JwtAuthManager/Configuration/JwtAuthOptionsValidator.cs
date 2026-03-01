using Microsoft.Extensions.Options;

namespace Linbik.JwtAuthManager.Configuration;

/// <summary>
/// Validates <see cref="JwtAuthOptions"/> configuration at startup.
/// Ensures all required values are provided and valid before application starts.
/// </summary>
public sealed class JwtAuthOptionsValidator : IValidateOptions<JwtAuthOptions>
{
    private const int MinSecretKeyLength = 32; // 256 bits minimum for HS256

    /// <summary>
    /// Validates the JWT authentication options configuration.
    /// </summary>
    /// <param name="name">The name of the options instance being validated.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
    public ValidateOptionsResult Validate(string? name, JwtAuthOptions options)
    {
        List<string> errors = [];

        // Validate SecretKey
        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            errors.Add("Linbik:JwtAuth:SecretKey is required. Use a cryptographically strong key (minimum 32 characters).");
        }
        else if (options.SecretKey.Length < MinSecretKeyLength)
        {
            errors.Add($"Linbik:JwtAuth:SecretKey must be at least {MinSecretKeyLength} characters for security. Current length: {options.SecretKey.Length}.");
        }

        // Validate JwtIssuer
        if (string.IsNullOrWhiteSpace(options.JwtIssuer))
        {
            errors.Add("Linbik:JwtAuth:JwtIssuer is required.");
        }

        // Validate JwtAudience
        if (string.IsNullOrWhiteSpace(options.JwtAudience))
        {
            errors.Add("Linbik:JwtAuth:JwtAudience is required.");
        }

        // Validate token lifetimes
        if (options.AccessTokenExpirationMinutes < 1 || options.AccessTokenExpirationMinutes > 1440)
        {
            errors.Add($"Linbik:JwtAuth:AccessTokenExpirationMinutes must be between 1 and 1440 (24 hours). Current value: {options.AccessTokenExpirationMinutes}.");
        }

        if (options.RefreshTokenExpirationDays < 1 || options.RefreshTokenExpirationDays > 365)
        {
            errors.Add($"Linbik:JwtAuth:RefreshTokenExpirationDays must be between 1 and 365. Current value: {options.RefreshTokenExpirationDays}.");
        }

        // Validate paths
        if (!options.LoginPath.StartsWith("/"))
        {
            errors.Add($"Linbik:JwtAuth:LoginPath must start with '/'. Current value: '{options.LoginPath}'.");
        }

        if (!options.LoginCallbackPath.StartsWith("/"))
        {
            errors.Add($"Linbik:JwtAuth:LoginCallbackPath must start with '/'. Current value: '{options.LoginCallbackPath}'.");
        }

        if (!options.LogoutPath.StartsWith("/"))
        {
            errors.Add($"Linbik:JwtAuth:LogoutPath must start with '/'. Current value: '{options.LogoutPath}'.");
        }

        if (!options.RefreshPath.StartsWith("/"))
        {
            errors.Add($"Linbik:JwtAuth:RefreshPath must start with '/'. Current value: '{options.RefreshPath}'.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
