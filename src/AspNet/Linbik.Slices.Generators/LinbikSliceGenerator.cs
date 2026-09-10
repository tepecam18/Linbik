using System.Linq;
using Microsoft.CodeAnalysis;

namespace Linbik.Slices.Generators;

/// <summary>
/// <c>[LinbikSlice]</c> ile işaretli tipleri tarayıp her biri için endpoint mapping
/// ve DI kayıtlarını üreten incremental source generator (Approach C).
/// Çıktı: tek bir <c>LinbikSlicesRegistry</c> sınıfı (<c>AddLinbikSlices</c> +
/// <c>MapLinbikSlices</c> uzantı metotları).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class LinbikSliceGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var slices = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                LinbikSliceConstants.SliceAttribute,
                predicate: static (node, _) => true,
                transform: static (ctx, _) => Extract(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        var collected = slices.Collect();
        var assemblyName = context.CompilationProvider.Select(static (c, _) => c.AssemblyName);
        var combined = collected.Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, pair) => LinbikSliceEmitter.Emit(spc, pair.Left, pair.Right));
    }

    private static SliceModel? Extract(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol slice)
            return null;

        var sliceAttr = ctx.Attributes.FirstOrDefault();
        if (sliceAttr is null)
            return null;

        // [LinbikSlice("/pattern")] + named Method/Tag
        var pattern = sliceAttr.ConstructorArguments.Length > 0
            ? sliceAttr.ConstructorArguments[0].Value as string
            : null;
        if (string.IsNullOrWhiteSpace(pattern))
            return null;

        var method = "POST";
        string? tag = null;
        string? summary = null;
        string? description = null;
        foreach (var named in sliceAttr.NamedArguments)
        {
            if (named.Key == "Method" && named.Value.Value is string m && !string.IsNullOrWhiteSpace(m))
                method = m;
            else if (named.Key == "Tag" && named.Value.Value is string t && !string.IsNullOrWhiteSpace(t))
                tag = t;
            else if (named.Key == "Summary" && named.Value.Value is string s && !string.IsNullOrWhiteSpace(s))
                summary = s;
            else if (named.Key == "Description" && named.Value.Value is string d && !string.IsNullOrWhiteSpace(d))
                description = d;
        }
        tag ??= slice.Name;

        // Flow metadata: [LFlowPublic] | [LFlow(...)] | [LAuthorizeFlow(...)] | (eksik → deny-by-default)
        var attributes = slice.GetAttributes();
        var hasPublic = attributes.Any(a => FullName(a.AttributeClass) == LinbikSliceConstants.FlowPublicAttribute);
        var flowAttr = attributes.FirstOrDefault(a => FullName(a.AttributeClass) == LinbikSliceConstants.FlowAttribute);
        var authorizeFlowAttr = attributes.FirstOrDefault(a => FullName(a.AttributeClass) == LinbikSliceConstants.AuthorizeFlowAttribute);

        string flowArgs;
        var isAuthorizeFlow = authorizeFlowAttr is not null;
        if (hasPublic)
        {
            flowArgs = "\"*\"";
        }
        else if ((authorizeFlowAttr ?? flowAttr) is { } declaredFlow)
        {
            // Both attributes serialize the same flow metadata; authorization is handled by the filter.
            flowArgs = string.Join(", ", ExtractFlowNames(declaredFlow).Select(f => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(f, true)));
        }
        else
        {
            // Eksik flow: analyzer LINBIK001 zaten hata verecek; yine de güvenli
            // (authenticated) üretip kodun derlenmesini engellememek için boş bırak.
            flowArgs = string.Empty;
        }

        // İç içe Request / Response / Handler / Validator çözümü
        string? requestFqn = null, responseFqn = null, handlerFqn = null, validatorFqn = null;
        foreach (var nested in slice.GetTypeMembers())
        {
            foreach (var iface in nested.AllInterfaces)
            {
                var def = iface.OriginalDefinition;
                var name = FullName(def);
                if (name == LinbikSliceConstants.RequestInterface)
                {
                    requestFqn = Fqn(nested);
                    responseFqn = Fqn(iface.TypeArguments[0]);
                }
                else if (name == LinbikSliceConstants.HandlerInterface)
                {
                    handlerFqn = Fqn(nested);
                }
                else if (name == LinbikSliceConstants.ValidatorInterface)
                {
                    validatorFqn = Fqn(nested);
                }
            }
        }

        var location = slice.Locations.FirstOrDefault() ?? Location.None;
        var valid = requestFqn is not null && responseFqn is not null && handlerFqn is not null;

        return new SliceModel(
            DisplayName: slice.Name,
            Pattern: pattern!,
            Method: method.ToUpperInvariant(),
            Tag: tag,
            Summary: summary,
            Description: description,
            FlowArgs: flowArgs,
            IsAuthorizeFlow: isAuthorizeFlow,
            RequestFqn: requestFqn,
            ResponseFqn: responseFqn,
            HandlerFqn: handlerFqn,
            ValidatorFqn: validatorFqn,
            Valid: valid,
            Location: location);
    }

    private static string FullName(INamedTypeSymbol? symbol)
    {
        if (symbol is null)
            return string.Empty;
        var ns = symbol.ContainingNamespace is { IsGlobalNamespace: false } n
            ? n.ToDisplayString() + "."
            : string.Empty;
        return ns + symbol.MetadataName;
    }

    private static string Fqn(ISymbol symbol)
        => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string? EnumConstantName(TypedConstant value)
    {
        if (value.Kind != TypedConstantKind.Enum || value.Type is not INamedTypeSymbol enumType || value.Value is null)
            return null;

        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue && Equals(member.ConstantValue, value.Value))
                return member.Name;
        }

        return null;
    }

    private static string[] ExtractFlowNames(AttributeData attr) =>
        attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Kind == TypedConstantKind.Array
            ? attr.ConstructorArguments[0].Values
                .Select(EnumConstantName)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToArray()
            : System.Array.Empty<string>();
}
