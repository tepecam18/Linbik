using System.Security.Claims;

namespace Linbik.Core.Responses;

public class TokenValidatorResponse
{
    public bool Success { get; set; } = false;
    public string? Message { get; set; }
    public IEnumerable<Claim>? Claims { get; set; }
    public Guid UserGuid { get; set; }
    public string? Name { get; set; }
}
