using AspNet.Models;
using Linbik.Core.Interfaces;
using Linbik.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AspNet.Controllers;

public class TestController(IAuthService authService) : Controller
{
    /// <summary>
    /// Ana dashboard sayfası - Kullanıcı durumu ve token bilgilerini gösterir
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var profile = await authService.GetUserProfileAsync(HttpContext);
        var tokens = await authService.GetIntegrationTokensAsync(HttpContext);
        
        var model = new DashboardViewModel
        {
            IsLoggedIn = profile != null,
            Profile = profile,
            Tokens = tokens ?? new List<LinbikIntegrationToken>()
        };
        
        return View(model);
    }
}
