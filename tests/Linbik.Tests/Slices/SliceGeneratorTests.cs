using Microsoft.CodeAnalysis;

namespace Linbik.Tests.Slices;

public sealed class SliceGeneratorTests
{
    private const string Public = "[LFlowPublic]";

    private static string Second(string flowAttributes, string name, string pattern) =>
        SliceCompilation.Slice(flowAttributes, name: name, pattern: pattern).Replace("using ", "//using ");

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void SliceMissingRequestOrHandlerReportsLinbik002(bool withRequest, bool withHandler)
    {
        var run = SliceCompilation.RunGenerator(
            SliceCompilation.Slice(Public, name: "Broken", withRequest: withRequest, withHandler: withHandler));

        var diagnostic = Assert.Single(run.GeneratorDiagnostics, d => d.Id == "LINBIK002");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Broken", diagnostic.GetMessage());
        Assert.Null(run.Generated);
    }

    [Fact]
    public void InvalidSliceIsLeftOutOfRegistryWhileValidOnesAreGenerated()
    {
        var source = SliceCompilation.Slice(Public, name: "GoodSlice", pattern: "/good") + "\n"
            + Second(Public, "BrokenSlice", "/broken").Replace("public sealed record Request() : ILinbikRequest<Response>;", string.Empty);

        var run = SliceCompilation.RunGenerator(source);

        Assert.Contains(run.GeneratorDiagnostics, d => d.Id == "LINBIK002" && d.GetMessage().Contains("BrokenSlice"));
        Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "LINBIK003");
        Assert.NotNull(run.Generated);
        Assert.Contains("GoodSlice", run.Generated);
        Assert.DoesNotContain("BrokenSlice", run.Generated);
    }

    [Fact]
    public void ProjectWithoutSlicesWarnsLinbik003AndGeneratesNothing()
    {
        var run = SliceCompilation.RunGenerator("public class Unrelated { }");

        var diagnostic = Assert.Single(run.GeneratorDiagnostics);
        Assert.Equal("LINBIK003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("GeneratorFixture", diagnostic.GetMessage());
        Assert.Null(run.Generated);
    }

    [Fact]
    public void LinbikSlicesAssemblyItselfDoesNotWarnAboutMissingSlices()
    {
        var run = SliceCompilation.RunGenerator("public class Unrelated { }", assemblyName: "Linbik.Slices");

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Null(run.Generated);
    }

    [Fact]
    public void SliceWithBlankPatternIsIgnored()
    {
        var source = SliceCompilation.Slice(Public, pattern: "  ");

        var run = SliceCompilation.RunGenerator(source);

        Assert.Contains(run.GeneratorDiagnostics, d => d.Id == "LINBIK003");
        Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "LINBIK002");
        Assert.Null(run.Generated);
    }

    [Theory]
    [InlineData("GET", "MapGet", true)]
    [InlineData("DELETE", "MapDelete", true)]
    [InlineData("HEAD", null, true)]
    [InlineData("POST", "MapPost", false)]
    [InlineData("PUT", "MapPut", false)]
    [InlineData("PATCH", "MapPatch", false)]
    public void HttpMethodDecidesBindingSourceAndMapMethod(string method, string? mapMethod, bool bodyless)
    {
        var run = SliceCompilation.RunGenerator(SliceCompilation.Slice(Public, method: method));

        Assert.Empty(run.CompilationErrors);
        Assert.NotNull(run.Generated);
        Assert.Contains(bodyless ? "[global::Microsoft.AspNetCore.Http.AsParameters]" : "[global::Microsoft.AspNetCore.Mvc.FromBody]", run.Generated);
        Assert.DoesNotContain(bodyless ? "Mvc.FromBody" : "Http.AsParameters", run.Generated);
        if (mapMethod is not null)
            Assert.Contains($"app.{mapMethod}(\"/demo\"", run.Generated);
    }

    [Fact]
    public void MethodNameIsCaseInsensitive()
    {
        var run = SliceCompilation.RunGenerator(SliceCompilation.Slice(Public, method: "get"));

        Assert.Contains("app.MapGet(", run.Generated);
        Assert.Contains("AsParameters", run.Generated);
    }

    [Fact]
    public void MethodDefaultsToPost()
    {
        var source = SliceCompilation.Slice(Public).Replace(", Method = \"GET\"", string.Empty);

        var run = SliceCompilation.RunGenerator(source);

        Assert.Contains("app.MapPost(", run.Generated);
        Assert.Contains("Mvc.FromBody", run.Generated);
    }

    [Fact]
    public void PublicSliceUsesFlowFilterWithWildcardMetadata()
    {
        var run = SliceCompilation.RunGenerator(SliceCompilation.Slice(Public));

        Assert.Empty(run.CompilationErrors);
        Assert.Contains("LFlowAuthorizeAttribute(\"*\")", run.Generated);
        Assert.Contains("AddEndpointFilter<global::Linbik.Slices.Endpoints.LinbikFlowEndpointFilter>", run.Generated);
        Assert.DoesNotContain("LinbikAuthorizeFlowEndpointFilter", run.Generated);
    }

    [Theory]
    [InlineData("[LFlow(LinbikFlow.Self)]", "LinbikFlowEndpointFilter", "LinbikAuthorizeFlowEndpointFilter", "\"Self\"")]
    [InlineData("[LAuthorizeFlow(LinbikFlow.Self)]", "LinbikAuthorizeFlowEndpointFilter", "LinbikFlowEndpointFilter>", "\"Self\"")]
    [InlineData("[LFlow(LinbikFlow.Self, LinbikFlow.Application)]", "LinbikFlowEndpointFilter", "LinbikAuthorizeFlowEndpointFilter", "\"Self\", \"Application\"")]
    public void FlowAttributeSelectsFilterAndSerializesFlowNames(
        string attribute, string expectedFilter, string unexpectedFilter, string expectedArgs)
    {
        var run = SliceCompilation.RunGenerator(SliceCompilation.Slice(attribute));

        Assert.Empty(run.CompilationErrors);
        Assert.Contains($"AddEndpointFilter<global::Linbik.Slices.Endpoints.{expectedFilter}>", run.Generated);
        Assert.DoesNotContain(unexpectedFilter, run.Generated);
        Assert.Contains($"LFlowAuthorizeAttribute({expectedArgs})", run.Generated);
    }

    // Characterization: the analyzer (LINBIK001) owns this error; the generator must stay
    // compilable and fall back to an empty flow list handled by the flow filter.
    [Fact]
    public void SliceWithoutFlowAttributeStillGeneratesCompilableCode()
    {
        var run = SliceCompilation.RunGenerator(SliceCompilation.Slice(string.Empty));

        Assert.Empty(run.CompilationErrors);
        Assert.Contains("LFlowAuthorizeAttribute()", run.Generated);
        Assert.Contains("LinbikFlowEndpointFilter", run.Generated);
    }

    [Fact]
    public void EndpointDocumentsOnlySuccessResponse()
    {
        var run = SliceCompilation.RunGenerator(SliceCompilation.Slice(Public));

        const string produces = "Produces<global::Linbik.Core.Responses.LBaseResponse<global::Demo.Response>>";
        Assert.Empty(run.CompilationErrors);
        Assert.NotNull(run.Generated);
        Assert.Equal(1, Count(run.Generated, "__e.Produces<"));
        Assert.Contains($"{produces}(200);", run.Generated);
    }

    [Fact]
    public void EndpointNameAndTagDefaultToSliceName()
    {
        var run = SliceCompilation.RunGenerator(SliceCompilation.Slice(Public, name: "CreateComment"));

        Assert.Contains("WithName(\"CreateComment\")", run.Generated);
        Assert.Contains("WithTags(\"CreateComment\")", run.Generated);
    }
}
