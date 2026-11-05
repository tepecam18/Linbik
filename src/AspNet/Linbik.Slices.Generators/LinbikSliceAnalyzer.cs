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
        ImmutableArray.Create(LinbikSliceConstants.MissingFlow, LinbikSliceConstants.ConflictingFlowDeclaration,
            LinbikSliceConstants.UnsupportedMethod, LinbikSliceConstants.IgnoredBodyBinding, LinbikSliceConstants.InvalidFlow);

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

        var location = type.Locations.FirstOrDefault() ?? Location.None;
        AnalyzeBinding(context, type, attributes.First(a => MatchesAttribute(a, LinbikSliceConstants.SliceAttribute)), location);
        foreach (var flow in attributes.Where(a => MatchesAttribute(a, LinbikSliceConstants.FlowAttribute) ||
                     MatchesAttribute(a, LinbikSliceConstants.AuthorizeFlowAttribute)))
        {
            foreach (var argument in flow.ConstructorArguments)
            {
                if (argument.Kind != TypedConstantKind.Array || argument.IsNull) continue;
                foreach (var value in argument.Values)
                {
                    if (value.Type is not INamedTypeSymbol enumType || value.Value is null) continue;
                    if (!enumType.GetMembers().OfType<IFieldSymbol>().Any(f => f.HasConstantValue && Equals(f.ConstantValue, value.Value)))
                        context.ReportDiagnostic(Diagnostic.Create(LinbikSliceConstants.InvalidFlow, location, type.Name, value.Value));
                }
            }
        }

        var flowAttributeCount = new[]
        {
            LinbikSliceConstants.FlowAttribute,
            LinbikSliceConstants.FlowPublicAttribute,
            LinbikSliceConstants.AuthorizeFlowAttribute,
        }.Count(name => attributes.Any(a => MatchesAttribute(a, name)));

        if (flowAttributeCount == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                LinbikSliceConstants.MissingFlow, location, type.Name));
            return;
        }

        if (flowAttributeCount > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                LinbikSliceConstants.ConflictingFlowDeclaration, location, type.Name));
        }
    }

    private static void AnalyzeBinding(SymbolAnalysisContext context, INamedTypeSymbol slice, AttributeData attribute, Location location)
    {
        var methodArgument = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Method");
        var method = methodArgument.Key is null ? "POST" : methodArgument.Value.Value as string;
        if (!LinbikSliceConstants.IsSupportedMethod(method))
        {
            context.ReportDiagnostic(Diagnostic.Create(LinbikSliceConstants.UnsupportedMethod, location, slice.Name, method ?? "null"));
            return;
        }
        if (method!.ToUpperInvariant() is "GET" or "DELETE" or "HEAD") return;
        foreach (var request in slice.GetTypeMembers().Where(t => t.AllInterfaces.Any(i =>
                     i.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).StartsWith("global::Linbik.Slices.ILinbikRequest<"))))
        {
            for (INamedTypeSymbol? current = request; current is not null; current = current.BaseType)
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            foreach (var binding in property.GetAttributes())
            {
                var name = binding.AttributeClass?.ToDisplayString();
                if (name is not ("Microsoft.AspNetCore.Mvc.FromRouteAttribute" or "Microsoft.AspNetCore.Mvc.FromQueryAttribute" or
                    "Microsoft.AspNetCore.Mvc.FromHeaderAttribute" or "Microsoft.AspNetCore.Mvc.FromFormAttribute" or
                    "Microsoft.AspNetCore.Mvc.FromServicesAttribute")) continue;
                context.ReportDiagnostic(Diagnostic.Create(LinbikSliceConstants.IgnoredBodyBinding,
                    property.Locations.FirstOrDefault() ?? location, slice.Name, method.ToUpperInvariant(),
                    binding.AttributeClass!.Name.Replace("Attribute", ""), property.Name));
            }
        }
    }

    private static bool MatchesAttribute(AttributeData attribute, string metadataName)
        => attribute.AttributeClass?.ToDisplayString() == metadataName;
}
