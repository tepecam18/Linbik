using Linbik.Core.Builders.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Linbik.Core.Builders;

public sealed class LinbikBuilder(IServiceCollection services) : ILinbikBuilder
{
    public IServiceCollection Services { get; } = services ?? throw new ArgumentNullException(nameof(services));
}