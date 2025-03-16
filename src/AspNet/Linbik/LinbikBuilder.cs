using Microsoft.Extensions.DependencyInjection;

namespace Linbik;
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