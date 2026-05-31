using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Linbik.Slices.Generators;

/// <summary>
/// Deny-by-default zorlaması (LINBIK001). <c>[LinbikSlice]</c> taşıyan ama
/// <c>[LFlow]</c>/<c>[LFlowPublic]</c> taşımayan tipler için derlemeyi kırar.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LinbikSliceAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(LinbikSliceConstants.MissingFlow);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        var attributes = type.GetAttributes();
        var hasSlice = attributes.Any(a => MatchesAttribute(a, LinbikSliceConstants.SliceAttribute));
        if (!hasSlice)
            return;

        var hasFlow = attributes.Any(a =>
            MatchesAttribute(a, LinbikSliceConstants.FlowAttribute) ||
            MatchesAttribute(a, LinbikSliceConstants.FlowPublicAttribute));

        if (hasFlow)
            return;

        var location = type.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
            LinbikSliceConstants.MissingFlow, location, type.Name));
    }

    private static bool MatchesAttribute(AttributeData attribute, string metadataName)
        => attribute.AttributeClass?.ToDisplayString() == metadataName;
}
