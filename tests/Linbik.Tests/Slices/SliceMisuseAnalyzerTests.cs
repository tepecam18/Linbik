using Microsoft.CodeAnalysis;

namespace Linbik.Tests.Slices;

public sealed class SliceMisuseAnalyzerTests
{
    [Theory]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    [InlineData("POSST")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvalidMethodsFailWithAnExampleAndNeverGeneratePostFallback(string method)
    {
        var source = SliceCompilation.Slice("[LFlowPublic]", method: method);
        var diagnostic = Assert.Single(await SliceCompilation.RunAnalyzerAsync(source));
        Assert.Equal("LINBIK005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Method = \"POST\"", diagnostic.GetMessage());
        var generated = SliceCompilation.RunGenerator(source);
        Assert.Contains(generated.GeneratorDiagnostics, d => d.Id == "LINBIK005");
        Assert.Null(generated.Generated);
    }

    [Theory]
    [InlineData("POST", "FromRoute")]
    [InlineData("PUT", "FromQuery")]
    [InlineData("PATCH", "FromHeader")]
    [InlineData("POST", "FromForm")]
    [InlineData("POST", "FromServices")]
    public async Task PropertyBindingOnJsonBodyWarnsWithCorrectAlternative(string method, string attribute)
    {
        var source = SliceCompilation.Slice("[LFlowPublic]", method: method)
            .Replace("record Request()", $"record Request([property: Microsoft.AspNetCore.Mvc.{attribute}] string Id)");
        var diagnostic = Assert.Single(await SliceCompilation.RunAnalyzerAsync(source));
        Assert.Equal("LINBIK006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Id", diagnostic.GetMessage());
        Assert.Contains("Request.RouteValues[\"id\"]", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    public async Task BodylessPropertyBindingIsAllowed(string method)
    {
        var source = SliceCompilation.Slice("[LFlowPublic]", method: method)
            .Replace("record Request()", "record Request([property: Microsoft.AspNetCore.Mvc.FromHeader] string Value)");
        Assert.Empty(await SliceCompilation.RunAnalyzerAsync(source));
    }

    [Theory]
    [InlineData("LFlow")]
    [InlineData("LAuthorizeFlow")]
    public async Task UndefinedFlowEnumFailsInsteadOfProducingUnreachableRoutes(string attribute)
    {
        var diagnostic = Assert.Single(await SliceCompilation.RunAnalyzerAsync(
            SliceCompilation.Slice($"[{attribute}((LinbikFlow)99)]")));
        Assert.Equal("LINBIK007", diagnostic.Id);
        Assert.Contains("LinbikFlow.Self", diagnostic.GetMessage());
        var generated = SliceCompilation.RunGenerator(SliceCompilation.Slice($"[{attribute}((LinbikFlow)99)]"));
        Assert.Contains(generated.GeneratorDiagnostics, d => d.Id == "LINBIK007");
        Assert.Null(generated.Generated);
    }
    [Fact]
    public async Task NullMethodIsRejectedInsteadOfFallingBackToPost()
    {
        var source = SliceCompilation.Slice("[LFlowPublic]").Replace("Method = \"GET\"", "Method = null");
        Assert.Contains(await SliceCompilation.RunAnalyzerAsync(source), d => d.Id == "LINBIK005");
        Assert.Null(SliceCompilation.RunGenerator(source).Generated);
    }

    [Fact]
    public async Task NullFlowArrayKeepsDocumentedAuthenticatedSemantics()
    {
        var source = SliceCompilation.Slice("[LAuthorizeFlow(null)]");
        Assert.Empty(await SliceCompilation.RunAnalyzerAsync(source));
        var run = SliceCompilation.RunGenerator(source);
        Assert.Empty(run.CompilationErrors);
        Assert.NotNull(run.Generated);
        Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

}
