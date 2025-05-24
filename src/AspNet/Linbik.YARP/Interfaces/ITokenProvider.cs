namespace Linbik.YARP.Interfaces;

public interface ITokenProvider
{
    Task<string> GetTokenAsync(string baseUrl, string clientId, string clientSecret);
}
