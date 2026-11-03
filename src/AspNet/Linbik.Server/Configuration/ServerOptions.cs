namespace Linbik.Server.Configuration;

/// <summary>Server-specific settings. Identity and validation settings belong to LinbikOptions.</summary>
public sealed class ServerOptions
{
    /// <summary>Base path for integration lifecycle webhook endpoints.</summary>
    public string IntegrationEndpointPath { get; set; } = "/api/Linbik";
}
