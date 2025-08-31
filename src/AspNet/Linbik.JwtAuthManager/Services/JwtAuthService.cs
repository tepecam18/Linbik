using Linbik.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Linbik.JwtAuthManager.Services;

class JwtAuthService : IAuthService
{
    private const string IdClaimType = "userId";

    public async Task<string> GetUserIdAsync(HttpContext context)
    {
        // Kullanıcı doğrulanmış mı kontrol ediyoruz
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            // En yaygın kullanılan claim türü: ClaimTypes.NameIdentifier
            var userId = context.User.FindFirst(IdClaimType)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                return userId;
            }
        }

        // Eğer kullanıcı doğrulanmamışsa veya id bulunamazsa null döner.
        return null;
    }
}
