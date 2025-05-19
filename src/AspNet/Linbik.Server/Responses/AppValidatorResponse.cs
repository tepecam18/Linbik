using System.Security.Claims;

namespace Linbik.Server.Responses;

public class AppValidatorResponse
{
    public bool success { get; set; }
    public List<Claim> claims { get; set; }
}
