using Microsoft.Extensions.DependencyInjection;
using Linbik.Core.Interfaces;

namespace Linbik.Core.Builders;

public class LinbikBuilder : ILinbikBuilder
{
    public IServiceCollection Services { get; }

    public LinbikBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }
}