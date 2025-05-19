
using Linbik.Server.Models;

namespace Linbik.Server.Interfaces;

public interface ILinbikServerRepository
{
    Task<LinbikAppModel> GetAppByGuid(Guid appGuid, string key);
}
