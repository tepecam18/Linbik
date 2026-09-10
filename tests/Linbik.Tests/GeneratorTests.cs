using Linbik.Slices.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Linbik.Tests;

public sealed class GeneratorTests
{
    [Theory]
    [InlineData("LFlow", "LinbikFlowEndpointFilter")]
    [InlineData("LAuthorizeFlow", "LinbikAuthorizeFlowEndpointFilter")]
    public void FlowVariantsGenerateCompilableEndpoints(string attribute, string filter)
    {
        var source = $$"""
            using System.Threading;
            using System.Threading.Tasks;
            using Linbik.Slices;
            using Linbik.Slices.Results;
            [LinbikSlice("/demo", Method = "GET")]
            [{{attribute}}(LinbikFlow.Self, LinbikFlow.Application)]
            public static class Demo {
                public sealed record Request() : ILinbikRequest<Response>;
                public sealed record Response(string Value);
                public sealed class Handler : ILinbikHandler<Request, Response> {
                    public ValueTask<Result<Response>> HandleAsync(Request request, CancellationToken ct)
                        => Result.Ok(new Response("ok")).AsValueTask();
                }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("GeneratorFixture", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new LinbikSliceGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Assert.DoesNotContain(diagnostics, x => x.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(output.GetDiagnostics(), x => x.Severity == DiagnosticSeverity.Error);
        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).ToString();
        Assert.Contains(filter, generated);
        Assert.Contains(nameof(Linbik.Slices.LinbikFlow.Application), generated);
    }
}
