namespace Linbik.JwtAuthManager.Models;

public class TokenModel
{
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public Guid UserGuid { get; set; }
    public string Name { get; set; } = string.Empty;
}
