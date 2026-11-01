using Linbik.Tests.Slices;
using Microsoft.CodeAnalysis;

namespace Linbik.Tests;

public sealed class GeneratorTests
{
    [Theory]
    [InlineData("LFlow", "LinbikFlowEndpointFilter")]
    [InlineData("LAuthorizeFlow", "LinbikAuthorizeFlowEndpointFilter")]
    public void FlowVariantsGenerateCompilableEndpoints(string attribute, string filter)
    {
        var source = SliceCompilation.Slice($"[{attribute}(LinbikFlow.Self, LinbikFlow.Application)]");
        var run = SliceCompilation.RunGenerator(source);
        Assert.DoesNotContain(run.GeneratorDiagnostics, x => x.Severity == DiagnosticSeverity.Error);
        Assert.Empty(run.CompilationErrors);
        Assert.NotNull(run.Generated);
        Assert.Contains(filter, run.Generated);
        Assert.Contains(nameof(Linbik.Slices.LinbikFlow.Application), run.Generated);
    }
}
