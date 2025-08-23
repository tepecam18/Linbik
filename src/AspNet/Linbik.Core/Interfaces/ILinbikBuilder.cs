using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Core.Interfaces;

public interface ILinbikBuilder
{
    IServiceCollection Services { get; }
}
