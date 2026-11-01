using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Linbik.Slices.CodeFixes;

/// <summary>
/// Quick fixes for LINBIK001 (add a flow attribute) and LINBIK004 (remove a conflicting one).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LinbikSliceCodeFixProvider)), Shared]
public sealed class LinbikSliceCodeFixProvider : CodeFixProvider
{
    private const string MissingFlowId = "LINBIK001";
    private const string ConflictingFlowId = "LINBIK004";

    private const string SliceAttributeName = "LinbikSlice";
    private const string FlowPublicName = "LFlowPublic";
    private const string FlowName = "LFlow";
    private const string AuthorizeFlowName = "LAuthorizeFlow";

    private static readonly ImmutableHashSet<string> FlowAttributeMetadataNames = ImmutableHashSet.Create(
        "Linbik.Slices.LFlowAttribute",
        "Linbik.Slices.LFlowPublicAttribute",
        "Linbik.Slices.LAuthorizeFlowAttribute");

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(MissingFlowId, ConflictingFlowId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var type = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<BaseTypeDeclarationSyntax>()
            .FirstOrDefault();
        if (type is null)
            return;

        if (diagnostic.Id == MissingFlowId)
        {
            RegisterAdd(context, type, "Add [LFlowPublic]", FlowPublicName, withSelfFlow: false);
            RegisterAdd(context, type, "Add [LFlow(LinbikFlow.Self)]", FlowName, withSelfFlow: true);
            RegisterAdd(context, type, "Add [LAuthorizeFlow(LinbikFlow.Self)]", AuthorizeFlowName, withSelfFlow: true);
            return;
        }

        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var symbol = semanticModel?.GetDeclaredSymbol(type, context.CancellationToken);
        if (symbol is null)
            return;

        foreach (var data in symbol.GetAttributes())
        {
            var attributeClass = data.AttributeClass;
            if (attributeClass is null || !FlowAttributeMetadataNames.Contains(attributeClass.ToDisplayString()))
                continue;

            if (data.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is not AttributeSyntax syntax
                || syntax.SyntaxTree != root.SyntaxTree)
                continue;

            var shortName = attributeClass.Name.EndsWith("Attribute", System.StringComparison.Ordinal)
                ? attributeClass.Name.Substring(0, attributeClass.Name.Length - "Attribute".Length)
                : attributeClass.Name;
            var title = $"Remove [{shortName}]";

            context.RegisterCodeFix(
                CodeAction.Create(title, ct => RemoveAttributeAsync(document, syntax, ct), equivalenceKey: title),
                diagnostic);
        }
    }

    private static void RegisterAdd(
        CodeFixContext context, BaseTypeDeclarationSyntax type, string title, string attributeName, bool withSelfFlow)
    {
        var document = context.Document;
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                ct => AddFlowAttributeAsync(document, type, attributeName, withSelfFlow, ct),
                equivalenceKey: title),
            context.Diagnostics[0]);
    }

    private static async Task<Document> AddFlowAttributeAsync(
        Document document, BaseTypeDeclarationSyntax type, string attributeName, bool withSelfFlow, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var prefix = GetNamespacePrefix(type);
        var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(prefix + attributeName));
        if (withSelfFlow)
        {
            attribute = attribute.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression(prefix + "LinbikFlow.Self")))));
        }

        var list = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));

        BaseTypeDeclarationSyntax updated;
        if (type.AttributeLists.Count > 0)
        {
            var last = type.AttributeLists[type.AttributeLists.Count - 1];
            list = list
                .WithLeadingTrivia(Indentation(last.GetLeadingTrivia()))
                .WithTrailingTrivia(last.GetTrailingTrivia().Where(IsWhitespaceOrEndOfLine));
            updated = type.WithAttributeLists(type.AttributeLists.Add(list));
        }
        else
        {
            var leading = type.GetLeadingTrivia();
            list = list
                .WithLeadingTrivia(leading)
                .WithTrailingTrivia(SyntaxFactory.EndOfLine(DetectEndOfLine(root)));
            updated = type
                .WithLeadingTrivia(Indentation(leading))
                .WithAttributeLists(type.AttributeLists.Add(list));
        }

        return document.WithSyntaxRoot(root.ReplaceNode(type, updated));
    }

    private static async Task<Document> RemoveAttributeAsync(
        Document document, AttributeSyntax attribute, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var list = (AttributeListSyntax)attribute.Parent!;
        var target = list.Attributes.Count == 1 ? (SyntaxNode)list : attribute;
        var keepComments = target == list && list.GetLeadingTrivia().Any(t => !IsWhitespaceOrEndOfLine(t));
        var newRoot = root.RemoveNode(
            target,
            keepComments ? SyntaxRemoveOptions.KeepLeadingTrivia : SyntaxRemoveOptions.KeepNoTrivia);

        return newRoot is null ? document : document.WithSyntaxRoot(newRoot);
    }

    // Mirrors how the existing [LinbikSlice] is written: qualified there means qualified here.
    private static string GetNamespacePrefix(BaseTypeDeclarationSyntax type)
    {
        foreach (var list in type.AttributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                if (attribute.Name is QualifiedNameSyntax qualified
                    && (qualified.Right.Identifier.ValueText == SliceAttributeName
                        || qualified.Right.Identifier.ValueText == SliceAttributeName + "Attribute"))
                {
                    return qualified.Left + ".";
                }
            }
        }

        return string.Empty;
    }

    private static SyntaxTriviaList Indentation(SyntaxTriviaList leading) =>
        leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(leading[leading.Count - 1])
            : default;

    private static bool IsWhitespaceOrEndOfLine(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia);

    private static string DetectEndOfLine(SyntaxNode root)
    {
        var eol = root.DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        return eol.IsKind(SyntaxKind.EndOfLineTrivia) ? eol.ToString() : "\n";
    }
}
