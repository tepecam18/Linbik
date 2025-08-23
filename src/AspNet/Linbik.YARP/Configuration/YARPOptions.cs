namespace Linbik.YARP.Configuration;

public class YARPOptions
{
    public string RouteId { get; set; } = string.Empty;
    public string ClusterId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public List<ClusterOptions> Clusters { get; set; } = new();
    public string PrefixPath { get; set; } = string.Empty;
}

public class ClusterOptions
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
