using Microsoft.IdentityModel.Tokens;

namespace Linbik.Server;

public class ServerOptions
{
    public string privateKey { get; set; }
    public string algorithm { get; set; } = SecurityAlgorithms.HmacSha512Signature;
    public string loginPath { get; set; } = "linbikApp/login";

    /// <summary>
    /// Default 60 minutes
    /// </summary>
    public int accessTokenExpiration { get; set; } = 60;
}
