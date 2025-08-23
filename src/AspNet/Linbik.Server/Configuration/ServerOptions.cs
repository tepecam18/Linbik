using Microsoft.IdentityModel.Tokens;

namespace Linbik.Server.Configuration;

public class ServerOptions
{
    public string PrivateKey { get; set; } = string.Empty;
    public string Algorithm { get; set; } = SecurityAlgorithms.HmacSha512Signature;
    public string LoginPath { get; set; } = "/linbik/app-login";

    /// <summary>
    /// Default 60 minutes
    /// </summary>
    public int AccessTokenExpiration { get; set; } = 60;
}
