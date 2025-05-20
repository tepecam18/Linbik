using Linbik.Server;
using Linbik.Server.Interfaces;
using Linbik.Server.Responses;

namespace AspNet.Repositories
{
    public class LinbikServerRepository : ILinbikServerRepository
    {
        public Task<AppValidatorResponse> AppLoginValidationsAsync(AppLoginRequest request)
        {
            return Task.FromResult(new AppValidatorResponse
            {
                success = true,
                claims = new()
            });
        }
    }
}
