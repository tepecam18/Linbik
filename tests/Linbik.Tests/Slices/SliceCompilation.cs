using System.Collections.Immutable;
using Linbik.Slices.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Linbik.Tests.Slices;

internal sealed record GeneratorRun(
    Compilation Output,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    string? Generated)
{
    public IEnumerable<Diagnostic> CompilationErrors =>
        Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
}

internal static class SliceCompilation
{
    private static readonly Lazy<MetadataReference[]> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray());

    public static IReadOnlyList<MetadataReference> MetadataReferences => References.Value;

    public static CSharpCompilation Create(string source, string assemblyName = "GeneratorFixture") =>
        CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    public static GeneratorRun RunGenerator(string source, string assemblyName = "GeneratorFixture")
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new LinbikSliceGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            Create(source, assemblyName), out var output, out var diagnostics);
        var generated = driver.GetRunResult().GeneratedTrees.SingleOrDefault()?.ToString();
        return new GeneratorRun(output, diagnostics, generated);
    }

    public static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string source)
    {
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new LinbikSliceAnalyzer());
        return await Create(source).WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>
    /// Builds a slice source file. <paramref name="flowAttributes"/> is emitted verbatim
    /// (e.g. <c>[LFlow(LinbikFlow.Self)]</c>); pass an empty string for none.
    /// </summary>
    public static string Slice(
        string flowAttributes,
        string method = "GET",
        string name = "Demo",
        string pattern = "/demo",
        bool withRequest = true,
        bool withHandler = true)
    {
        var request = withRequest
            ? "public sealed record Request() : ILinbikRequest<Response>;"
            : "public sealed record NotARequest();";
        var handler = withHandler
            ? """
              public sealed class Handler : ILinbikHandler<Request, Response> {
                  public ValueTask<Result<Response>> HandleAsync(Request request, CancellationToken ct)
                      => Result.Ok(new Response("ok")).AsValueTask();
              }
              """
            : string.Empty;

        return $$"""
            using System.Threading;
            using System.Threading.Tasks;
            using Linbik.Slices;
            using Linbik.Slices.Results;
            [LinbikSlice("{{pattern}}", Method = "{{method}}")]
            {{flowAttributes}}
            public static class {{name}} {
                {{request}}
                public sealed record Response(string Value);
                {{handler}}
            }
            """;
    }
}
