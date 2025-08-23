using Microsoft.IdentityModel.Tokens;

namespace Linbik.JwtAuthManager.Configuration;

public class JwtAuthOptions
{
    public string PrivateKey { get; set; } = string.Empty;
    public string Algorithm { get; set; } = SecurityAlgorithms.HmacSha512Signature;
    public string LoginPath { get; set; } = "/linbik/login";
    public string RefreshLoginPath { get; set; } = "/linbik/refresh-token";
    public string ExitPath { get; set; } = "/linbik/logout";
    public string PkceStartPath { get; set; } = "/linbik/pkce-start";

    public bool PkceEnabled { get; set; } = true;

    /// <summary>
    /// Default 15 minutes
    /// </summary>
    public int AccessTokenExpiration { get; set; } = 15;
    
    /// <summary>
    /// Default 15 days
    /// </summary>
    public int RefreshTokenExpiration { get; set; } = 15;

    public Dictionary<string, string> Routes { get; set; } = new();

    public bool RefererControl { get; set; } = false;
}
