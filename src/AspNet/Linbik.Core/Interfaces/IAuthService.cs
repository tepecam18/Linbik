using Microsoft.AspNetCore.Http;

namespace Linbik.Interfaces;

public interface IAuthService
{
    public Task<string> GetUserIdAsync(HttpContext context);
}
