
namespace Linbik.Server.Interfaces;

interface ILinbikServerRepository
{
    Task<bool> AppLoginValidations(Guid appGuid, string key);
}
