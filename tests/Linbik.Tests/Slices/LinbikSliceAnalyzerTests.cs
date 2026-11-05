using Linbik.Slices.Generators;
using Microsoft.CodeAnalysis;

namespace Linbik.Tests.Slices;

public sealed class LinbikSliceAnalyzerTests
{
    private const string Flow = "[LFlow(LinbikFlow.Self)]";
    private const string Public = "[LFlowPublic]";
    private const string Authorize = "[LAuthorizeFlow(LinbikFlow.Self)]";

    private static string Ids(IEnumerable<Diagnostic> diagnostics) =>
        string.Join(",", diagnostics.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal));

    [Fact]
    public async Task SliceWithoutFlowAttributeReportsLinbik001AtTypeName()
    {
        var source = SliceCompilation.Slice(string.Empty, name: "OrphanSlice");

        var diagnostic = Assert.Single(await SliceCompilation.RunAnalyzerAsync(source));

        Assert.Equal("LINBIK001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("OrphanSlice", diagnostic.GetMessage());
        Assert.Equal("OrphanSlice", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
    }

    [Theory]
    [InlineData(Flow)]
    [InlineData(Public)]
    [InlineData(Authorize)]
    [InlineData("[LFlow]")]
    [InlineData("[LAuthorizeFlow]")]
    public async Task SliceWithExactlyOneFlowAttributeReportsNothing(string flowAttribute)
    {
        var source = SliceCompilation.Slice(flowAttribute);

        Assert.Empty(await SliceCompilation.RunAnalyzerAsync(source));
    }

    [Theory]
    [InlineData(Flow, Public)]
    [InlineData(Flow, Authorize)]
    [InlineData(Public, Authorize)]
    [InlineData(Public, Flow)]
    public async Task SliceWithTwoFlowAttributesReportsLinbik004Only(string first, string second)
    {
        var source = SliceCompilation.Slice($"{first}\n{second}");

        var diagnostic = Assert.Single(await SliceCompilation.RunAnalyzerAsync(source));

        Assert.Equal("LINBIK004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Demo", diagnostic.GetMessage());
    }

    [Fact]
    public async Task SliceWithAllThreeFlowAttributesReportsSingleLinbik004()
    {
        var source = SliceCompilation.Slice($"{Flow}\n{Public}\n{Authorize}");

        Assert.Equal("LINBIK004", Ids(await SliceCompilation.RunAnalyzerAsync(source)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(Flow)]
    [InlineData(Public)]
    [InlineData(Authorize)]
    public async Task TypeWithoutSliceAttributeIsIgnored(string flowAttribute)
    {
        var source = $$"""
            using Linbik.Slices;
            {{flowAttribute}}
            public static class NotASlice { }
            """;

        Assert.Empty(await SliceCompilation.RunAnalyzerAsync(source));
    }

    [Fact]
    public async Task NonStaticClassIsAnalyzedToo()
    {
        var source = """
            using Linbik.Slices;
            [LinbikSlice("/x")]
            public sealed class Instance { }
            """;

        Assert.Equal("LINBIK001", Ids(await SliceCompilation.RunAnalyzerAsync(source)));
    }

    [Fact]
    public async Task PartialSliceWithFlowOnAnotherPartReportsNothing()
    {
        var source = """
            using Linbik.Slices;
            [LinbikSlice("/x")]
            public static partial class Split { }
            [LFlowPublic]
            public static partial class Split { }
            """;

        Assert.Empty(await SliceCompilation.RunAnalyzerAsync(source));
    }

    [Fact]
    public async Task NestedSliceIsAnalyzed()
    {
        var source = """
            using Linbik.Slices;
            public static class Outer
            {
                [LinbikSlice("/x")]
                public static class Inner { }
            }
            """;

        var diagnostic = Assert.Single(await SliceCompilation.RunAnalyzerAsync(source));

        Assert.Equal("LINBIK001", diagnostic.Id);
        Assert.Contains("Inner", diagnostic.GetMessage());
    }

    [Fact]
    public async Task EachSliceIsReportedIndependently()
    {
        var source = SliceCompilation.Slice(string.Empty, name: "A") + "\n"
            + SliceCompilation.Slice($"{Flow}\n{Public}", name: "B", pattern: "/b").Replace("using ", "//using ");

        var diagnostics = await SliceCompilation.RunAnalyzerAsync(source);

        Assert.Equal("LINBIK001,LINBIK004", Ids(diagnostics));
    }

    [Fact]
    public void AnalyzerSupportsFlowAndBindingDiagnostics()
    {
        var supported = new LinbikSliceAnalyzer().SupportedDiagnostics.Select(d => d.Id);

        Assert.Equal(["LINBIK001", "LINBIK004", "LINBIK005", "LINBIK006", "LINBIK007"], supported.OrderBy(id => id, StringComparer.Ordinal));
    }
}
