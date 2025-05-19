using Linbik.Server.Responses;

namespace Linbik.Server.Interfaces;

public interface ILinbikServerRepository
{
    Task<AppValidatorResponse> AppLoginValidationsAsync(AppLoginRequest request);
}
