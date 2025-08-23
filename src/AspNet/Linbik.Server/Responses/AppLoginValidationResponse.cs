using System.Security.Claims;

namespace Linbik.Server.Responses;

public class AppLoginValidationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<Claim> Claims { get; set; } = new();
}
