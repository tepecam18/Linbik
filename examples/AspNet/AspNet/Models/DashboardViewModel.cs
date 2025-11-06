using Linbik.Core.Interfaces;

namespace AspNet.Models;

public class DashboardViewModel
{
    public bool IsLoggedIn { get; set; }
    public UserProfile? Profile { get; set; }
    public List<IntegrationToken> Tokens { get; set; } = new();
}
