namespace Linbik.JwtAuthManager.Models;

/// <summary>
/// Login redirect response for mobile clients
/// </summary>
public sealed class LoginResponse
{
    public string? RedirectPath { get; set; }
}
