using Linbik.Server.Interfaces;
using Linbik.Server.Models;
using Linbik.Server.Responses;

namespace AspNet.Repositories
{
    public class LinbikServerRepository : ILinbikServerRepository
    {
        public Task<AppLoginValidationResponse> AppLoginValidationsAsync(AppLoginModel request)
        {
            return Task.FromResult(new AppLoginValidationResponse
            {
                Success = true,
                Claims = new()
            });
        }
    }
}
