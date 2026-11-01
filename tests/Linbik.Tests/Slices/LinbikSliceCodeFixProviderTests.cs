using Linbik.Slices.CodeFixes;
using Linbik.Slices.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Linbik.Tests.Slices;

public sealed class LinbikSliceCodeFixProviderTests
{
    private sealed record AppliedFix(string Title, string Text, string[] RemainingLinbikDiagnostics, string[] CompilationErrors);

    private static async Task<List<AppliedFix>> ApplyFixesAsync(string source, string diagnosticId)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        workspace.AddProject(ProjectInfo.Create(
            projectId, VersionStamp.Create(), "P", "P", LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: SliceCompilation.MetadataReferences));
        var document = workspace.AddDocument(projectId, "Slice.cs", SourceText.From(source));

        var diagnostics = await AnalyzeAsync(document.Project);
        var diagnostic = diagnostics.Single(d => d.Id == diagnosticId);

        var actions = new List<CodeAction>();
        await new LinbikSliceCodeFixProvider().RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));

        var applied = new List<AppliedFix>();
        foreach (var action in actions)
        {
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
            var text = (await changed.GetTextAsync()).ToString().Replace("\r\n", "\n");

            var remaining = await AnalyzeAsync(changed.Project);
            var compilation = (await changed.Project.GetCompilationAsync())!;
            applied.Add(new AppliedFix(
                action.Title,
                text,
                remaining.Select(d => d.Id).ToArray(),
                compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray()));
        }

        return applied;
    }

    private static async Task<IReadOnlyList<Diagnostic>> AnalyzeAsync(Project project)
    {
        var compilation = (await project.GetCompilationAsync())!;
        return await compilation
            .WithAnalyzers([new LinbikSliceAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
    }

    private const string MissingFlowSource = """
        using Linbik.Slices;

        [LinbikSlice("/x")]
        public static class X { }
        """;

    [Fact]
    public void FixableDiagnosticIdsAreExactlyTheAnalyzerOnes()
    {
        var ids = new LinbikSliceCodeFixProvider().FixableDiagnosticIds;

        Assert.Equal(["LINBIK001", "LINBIK004"], ids.OrderBy(i => i));
    }

    [Fact]
    public async Task MissingFlowOffersTheThreeFlowChoices()
    {
        var fixes = await ApplyFixesAsync(MissingFlowSource, "LINBIK001");

        Assert.Equal(
            ["Add [LFlowPublic]", "Add [LFlow(LinbikFlow.Self)]", "Add [LAuthorizeFlow(LinbikFlow.Self)]"],
            fixes.Select(f => f.Title));
    }

    [Theory]
    [InlineData("Add [LFlowPublic]", "[LFlowPublic]")]
    [InlineData("Add [LFlow(LinbikFlow.Self)]", "[LFlow(LinbikFlow.Self)]")]
    [InlineData("Add [LAuthorizeFlow(LinbikFlow.Self)]", "[LAuthorizeFlow(LinbikFlow.Self)]")]
    public async Task MissingFlowFixAddsAttributeOnItsOwnLineAndResolvesTheDiagnostic(string title, string attribute)
    {
        var fix = (await ApplyFixesAsync(MissingFlowSource, "LINBIK001")).Single(f => f.Title == title);

        Assert.Equal(
            $"using Linbik.Slices;\n\n[LinbikSlice(\"/x\")]\n{attribute}\npublic static class X {{ }}",
            fix.Text);
        Assert.Empty(fix.RemainingLinbikDiagnostics);
        Assert.Empty(fix.CompilationErrors);
    }

    [Fact]
    public async Task MissingFlowFixUsesQualifiedNamesWhenSliceAttributeIsQualified()
    {
        const string source = """
            [Linbik.Slices.LinbikSlice("/x")]
            public static class X { }
            """;

        var fixes = await ApplyFixesAsync(source, "LINBIK001");

        var flow = fixes.Single(f => f.Title == "Add [LFlow(LinbikFlow.Self)]");
        Assert.Contains("[Linbik.Slices.LFlow(Linbik.Slices.LinbikFlow.Self)]", flow.Text);
        Assert.All(fixes, f => Assert.Empty(f.CompilationErrors));
        Assert.All(fixes, f => Assert.Empty(f.RemainingLinbikDiagnostics));
    }

    [Fact]
    public async Task MissingFlowFixKeepsIndentationOfNestedType()
    {
        const string source = """
            using Linbik.Slices;

            namespace App
            {
                public static class Outer
                {
                    [LinbikSlice("/x")]
                    public static class X { }
                }
            }
            """;

        var fix = (await ApplyFixesAsync(source, "LINBIK001")).Single(f => f.Title == "Add [LFlowPublic]");

        Assert.Contains("        [LinbikSlice(\"/x\")]\n        [LFlowPublic]\n        public static class X { }", fix.Text);
        Assert.Empty(fix.RemainingLinbikDiagnostics);
    }

    [Fact]
    public async Task MissingFlowFixWorksWhenAttributeSharesTheTypeLine()
    {
        const string source = """
            using Linbik.Slices;

            [LinbikSlice("/x")] public static class X { }
            """;

        var fix = (await ApplyFixesAsync(source, "LINBIK001")).Single(f => f.Title == "Add [LFlowPublic]");

        Assert.Empty(fix.RemainingLinbikDiagnostics);
        Assert.Empty(fix.CompilationErrors);
    }

    [Fact]
    public async Task MissingFlowFixPreservesDocCommentAboveTheAttributes()
    {
        const string source = """
            using Linbik.Slices;

            /// <summary>Docs.</summary>
            [LinbikSlice("/x")]
            public static class X { }
            """;

        var fix = (await ApplyFixesAsync(source, "LINBIK001")).Single(f => f.Title == "Add [LFlowPublic]");

        Assert.StartsWith("using Linbik.Slices;\n\n/// <summary>Docs.</summary>\n[LinbikSlice(\"/x\")]\n[LFlowPublic]\n", fix.Text);
        Assert.Empty(fix.RemainingLinbikDiagnostics);
    }

    [Fact]
    public async Task ConflictingFlowsOfferOneRemoveFixPerAttribute()
    {
        const string source = """
            using Linbik.Slices;

            [LinbikSlice("/x")]
            [LFlow(LinbikFlow.Self)]
            [LFlowPublic]
            [LAuthorizeFlow(LinbikFlow.Self)]
            public static class X { }
            """;

        var fixes = await ApplyFixesAsync(source, "LINBIK004");

        Assert.Equal(
            new[] { "Remove [LAuthorizeFlow]", "Remove [LFlow]", "Remove [LFlowPublic]" }.ToHashSet(),
            fixes.Select(f => f.Title).ToHashSet());
        Assert.Equal(3, fixes.Count);
    }

    [Fact]
    public async Task RemoveFixDeletesOnlyThatAttributeLine()
    {
        const string source = """
            using Linbik.Slices;

            [LinbikSlice("/x")]
            [LFlow(LinbikFlow.Self)]
            [LFlowPublic]
            public static class X { }
            """;

        var fix = (await ApplyFixesAsync(source, "LINBIK004")).Single(f => f.Title == "Remove [LFlowPublic]");

        Assert.Equal(
            "using Linbik.Slices;\n\n[LinbikSlice(\"/x\")]\n[LFlow(LinbikFlow.Self)]\npublic static class X { }",
            fix.Text);
        Assert.Empty(fix.RemainingLinbikDiagnostics);
        Assert.Empty(fix.CompilationErrors);
    }

    [Fact]
    public async Task RemoveFixKeepsOtherAttributesInTheSameList()
    {
        const string source = """
            using Linbik.Slices;

            [LinbikSlice("/x"), LFlow(LinbikFlow.Self), LFlowPublic]
            public static class X { }
            """;

        var fix = (await ApplyFixesAsync(source, "LINBIK004")).Single(f => f.Title == "Remove [LFlow]");

        Assert.Contains("[LinbikSlice(\"/x\"), LFlowPublic]", fix.Text);
        Assert.Empty(fix.RemainingLinbikDiagnostics);
        Assert.Empty(fix.CompilationErrors);
    }

    [Fact]
    public async Task RemoveFixKeepsDocCommentWhenFirstAttributeListGoesAway()
    {
        const string source = """
            using Linbik.Slices;

            /// <summary>Docs.</summary>
            [LFlowPublic]
            [LinbikSlice("/x")]
            [LFlow(LinbikFlow.Self)]
            public static class X { }
            """;

        var fix = (await ApplyFixesAsync(source, "LINBIK004")).Single(f => f.Title == "Remove [LFlowPublic]");

        Assert.Contains("/// <summary>Docs.</summary>", fix.Text);
        Assert.DoesNotContain("LFlowPublic", fix.Text);
        Assert.Empty(fix.RemainingLinbikDiagnostics);
        Assert.Empty(fix.CompilationErrors);
    }

    [Fact]
    public async Task EveryConflictFixResolvesTheDiagnostic()
    {
        const string source = """
            using Linbik.Slices;

            [LinbikSlice("/x")]
            [LFlow(LinbikFlow.Self)]
            [LFlowPublic]
            public static class X { }
            """;

        var fixes = await ApplyFixesAsync(source, "LINBIK004");

        Assert.Equal(2, fixes.Count);
        Assert.All(fixes, f => Assert.Empty(f.RemainingLinbikDiagnostics));
        Assert.All(fixes, f => Assert.Empty(f.CompilationErrors));
    }
}
