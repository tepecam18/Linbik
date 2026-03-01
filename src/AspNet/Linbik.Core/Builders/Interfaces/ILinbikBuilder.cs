using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Core.Builders.Interfaces;

public interface ILinbikBuilder
{
    IServiceCollection Services { get; }
}
