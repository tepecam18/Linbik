namespace Linbik.Core.Responses;

public class TokenValidatorResponse
{
    public Guid UserGuid { get; set; }
    public string? AppId { get; set; }
    public string? Name { get; set; }
    public bool Success { get; set; } = false;
    public string? Message { get; set; }
}
