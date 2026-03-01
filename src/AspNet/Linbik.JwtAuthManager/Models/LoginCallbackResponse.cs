namespace Linbik.JwtAuthManager.Models;

/// <summary>
/// Login callback response containing user info and integration data
/// </summary>
public sealed class LoginCallbackResponse
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Integrations { get; set; } = [];
    public string? RedirectPath { get; set; }
}
