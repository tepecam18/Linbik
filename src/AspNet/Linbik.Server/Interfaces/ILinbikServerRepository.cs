using Linbik.Server.Models;
using Linbik.Server.Responses;

namespace Linbik.Server.Interfaces;

public interface ILinbikServerRepository
{
    Task<AppLoginValidationResponse> AppLoginValidationsAsync(AppLoginModel request);
}
