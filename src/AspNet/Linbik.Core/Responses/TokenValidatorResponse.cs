namespace Linbik.Core.Responses;

public class TokenValidatorResponse
{
    public Guid UserGuid { get; internal set; }
    public string? AppId { get; internal set; }
    public string? Name { get; internal set; }
    public bool Success { get; internal set; } = false;
    public string? Message { get; internal set; }
}
