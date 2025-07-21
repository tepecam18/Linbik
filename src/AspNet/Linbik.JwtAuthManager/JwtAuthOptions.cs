using Microsoft.IdentityModel.Tokens;

namespace Linbik.JwtAuthManager;

public class JwtAuthOptions
{
    public string privateKey { get; set; }
    public string algorithm { get; set; } = SecurityAlgorithms.HmacSha512Signature;
    public string loginPath { get; set; } = "/linbik/login";
    public string refreshLoginPath { get; set; } = "/linbik/refresh-login";
    public string exitPath { get; set; } = "/linbik/logout";

    public bool refreshLoginPathEnabled { get; set; } = true;

    //public string redirectUrl { get; set; }
    //public string secret { get; set; }
    //public string issuer { get; set; }
    //public string audience { get; set; }
    /// <summary>
    /// Default 15 minutes
    /// </summary>
    public int accessTokenExpiration { get; set; } = 15;
    /// <summary>
    /// Default 15 days
    /// </summary>
    public int refreshTokenExpiration { get; set; } = 15;
    //public bool allowMultipleLoginsFromTheSameUser { get; set; } = false;
    //public bool allowSignOutAllUserActiveClients { get; set; } = false;
    //public bool allowSignOutAllUserActiveClientsExceptCurrent { get; set; } = false;
    //public bool validateIssuer { get; set; } = false;
    //public bool validateAudience { get; set; } = false;
    //public bool validateLifetime { get; set; } = false;
    //public bool validateIssuerSigningKey { get; set; } = false;
    //public bool requireExpirationTime { get; set; } = false;
    //public bool requireSignedTokens { get; set; } = false;
    //public bool requireAudience { get; set; } = false;
    //public bool requireIssuer { get; set; } = false;
    //public bool requireHttpsMetadata { get; set; } = false;

    public Dictionary<string, string> routes { get; set; }
}
