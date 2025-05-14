using Microsoft.AspNetCore.Http;

namespace Linbik.Core.Interfaces;

public interface IAuthService
{
    public Task<string> GetUserIdAsync(HttpContext context);
}
