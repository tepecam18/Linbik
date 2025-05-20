namespace Linbik.JwtAuthManager;

public class YARPOptions
{
    public string RouteId { get; set; }
    public string ClusterId { get; set; }
    public string privateKey { get; set; }
    public List<ClusterOptions> clusters { get; set; }
    public string prefixPath { get; set; } = "";

}


public class ClusterOptions
{
    public string name { get; set; }
    public string address { get; set; }
}
