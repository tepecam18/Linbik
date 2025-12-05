using Linbik.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Core.Builders;

public class LinbikBuilder : ILinbikBuilder
{
    public IServiceCollection Services { get; }

    public LinbikBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }
}