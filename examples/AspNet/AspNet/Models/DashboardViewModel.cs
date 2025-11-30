using Linbik.Core.Interfaces;
using Linbik.Core.Models;

namespace AspNet.Models;

public class DashboardViewModel
{
    public bool IsLoggedIn { get; set; }
    public UserProfile? Profile { get; set; }
    public List<LinbikIntegrationToken> Tokens { get; set; } = new();
}
