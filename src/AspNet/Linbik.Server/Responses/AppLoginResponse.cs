namespace Linbik.Server.Responses;

public class AppLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}
