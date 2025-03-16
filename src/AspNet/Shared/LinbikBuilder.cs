using Microsoft.Extensions.DependencyInjection;

namespace Shared;
public interface ILinbikBuilder
{
    IServiceCollection Services { get; }
}

public class LinbikBuilder : ILinbikBuilder
{
    public IServiceCollection Services { get; }

    public LinbikBuilder(IServiceCollection services)
    {
        Services = services;
    }
}