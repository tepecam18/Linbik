using Linbik.Core.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Linbik.PasetoAuthManager.Configuration;

/// <summary>
/// Validates <see cref="PasetoAuthOptions"/> configuration at startup.
/// Branches by <see cref="PasetoAuthOptions.Mode"/>: v4.public requires Ed25519 key pair,
/// v4.local requires a 32-byte symmetric shared key.
/// </summary>
public sealed class PasetoAuthOptionsValidator : IValidateOptions<PasetoAuthOptions>
{
    private const int Ed25519PrivateKeyBytes = 64; // seed (32) + public (32)
    private const int Ed25519PublicKeyBytes = 32;
    private const int SymmetricSharedKeyBytes = 32;

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, PasetoAuthOptions options)
    {
        List<string> errors = [];

        if (options.Mode == PasetoMode.Local)
        {
            // SharedKey
            if (string.IsNullOrWhiteSpace(options.SharedKeyBase64))
            {
                errors.Add("Linbik:PasetoAuth:SharedKeyBase64 is required when Mode=Local. Generate via IPasetoHelper.GenerateSymmetricKey().");
            }
            else if (!TryDecodeKey(options.SharedKeyBase64, SymmetricSharedKeyBytes, out var sharedErr))
            {
                errors.Add($"Linbik:PasetoAuth:SharedKeyBase64 is invalid: {sharedErr}");
            }
        }
        else
        {
            // PrivateKey
            if (string.IsNullOrWhiteSpace(options.PrivateKeyBase64))
            {
                errors.Add("Linbik:PasetoAuth:PrivateKeyBase64 is required when Mode=Public. Generate via IPasetoHelper.GenerateKeyPair().");
            }
            else if (!TryDecodeKey(options.PrivateKeyBase64, Ed25519PrivateKeyBytes, out var privErr))
            {
                errors.Add($"Linbik:PasetoAuth:PrivateKeyBase64 is invalid: {privErr}");
            }

            // PublicKey
            if (string.IsNullOrWhiteSpace(options.PublicKeyBase64))
            {
                errors.Add("Linbik:PasetoAuth:PublicKeyBase64 is required when Mode=Public. Generate via IPasetoHelper.GenerateKeyPair().");
            }
            else if (!TryDecodeKey(options.PublicKeyBase64, Ed25519PublicKeyBytes, out var pubErr))
            {
                errors.Add($"Linbik:PasetoAuth:PublicKeyBase64 is invalid: {pubErr}");
            }
        }

        // Issuer
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            errors.Add("Linbik:PasetoAuth:Issuer is required.");
        }

        // Audience
        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add("Linbik:PasetoAuth:Audience is required.");
        }

        // Lifetimes
        if (options.AccessTokenExpirationMinutes is < 1 or > 1440)
        {
            errors.Add($"Linbik:PasetoAuth:AccessTokenExpirationMinutes must be between 1 and 1440. Current: {options.AccessTokenExpirationMinutes}.");
        }

        if (options.RefreshTokenExpirationDays is < 1 or > 365)
        {
            errors.Add($"Linbik:PasetoAuth:RefreshTokenExpirationDays must be between 1 and 365. Current: {options.RefreshTokenExpirationDays}.");
        }

        // Paths
        if (!options.LoginPath.StartsWith('/'))
            errors.Add($"Linbik:PasetoAuth:LoginPath must start with '/'. Current: '{options.LoginPath}'.");
        if (!options.LoginCallbackPath.StartsWith('/'))
            errors.Add($"Linbik:PasetoAuth:LoginCallbackPath must start with '/'. Current: '{options.LoginCallbackPath}'.");
        if (!options.LogoutPath.StartsWith('/'))
            errors.Add($"Linbik:PasetoAuth:LogoutPath must start with '/'. Current: '{options.LogoutPath}'.");
        if (!options.RefreshPath.StartsWith('/'))
            errors.Add($"Linbik:PasetoAuth:RefreshPath must start with '/'. Current: '{options.RefreshPath}'.");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static bool TryDecodeKey(string base64, int expectedBytes, out string error)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length != expectedBytes)
            {
                error = $"expected {expectedBytes}-byte key, got {bytes.Length} bytes.";
                return false;
            }
            error = string.Empty;
            return true;
        }
        catch (FormatException)
        {
            error = "value is not valid Base64.";
            return false;
        }
    }
}
