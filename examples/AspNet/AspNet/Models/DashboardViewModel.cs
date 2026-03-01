using Linbik.Core.Models;
using Linbik.Core.Services.Interfaces;

namespace AspNet.Models;

public sealed class DashboardViewModel
{
    public bool IsLoggedIn { get; set; }
    public UserProfile? Profile { get; set; }
    public List<LinbikIntegrationToken> Tokens { get; set; } = [];
}
