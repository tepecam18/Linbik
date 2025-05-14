using Linbik.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Linbik.JwtAuthManager;

class JwtAuthService : IAuthService
{
    public async Task<string> GetUserIdAsync(HttpContext context)
    {
        // Kullanıcı doğrulanmış mı kontrol ediyoruz
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            // En yaygın kullanılan claim türü: ClaimTypes.NameIdentifier
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                return userId;
            }
        }

        // Eğer kullanıcı doğrulanmamışsa veya id bulunamazsa null döner.
        return null;
    }
}
