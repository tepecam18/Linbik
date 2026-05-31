using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

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

        context.RegisterSourceOutput(collected, static (spc, models) => Emit(spc, models));
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
        foreach (var named in sliceAttr.NamedArguments)
        {
            if (named.Key == "Method" && named.Value.Value is string m && !string.IsNullOrWhiteSpace(m))
                method = m;
            else if (named.Key == "Tag" && named.Value.Value is string t && !string.IsNullOrWhiteSpace(t))
                tag = t;
        }
        tag ??= slice.Name;

        // Flow metadata: [LFlowPublic] | [LFlow(...)] | (eksik → deny-by-default)
        var attributes = slice.GetAttributes();
        var hasPublic = attributes.Any(a => FullName(a.AttributeClass) == LinbikSliceConstants.FlowPublicAttribute);
        var flowAttr = attributes.FirstOrDefault(a => FullName(a.AttributeClass) == LinbikSliceConstants.FlowAttribute);

        string flowArgs;
        if (hasPublic)
        {
            flowArgs = "\"*\"";
        }
        else if (flowAttr is not null)
        {
            var flows = flowAttr.ConstructorArguments.Length > 0 && flowAttr.ConstructorArguments[0].Kind == TypedConstantKind.Array
                ? flowAttr.ConstructorArguments[0].Values
                    .Select(v => v.Value as string)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray()
                : System.Array.Empty<string>();

            flowArgs = flows.Length == 0
                ? string.Empty
                : string.Join(", ", flows.Select(f => "\"" + f!.Replace("\"", "\\\"") + "\""));
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
            FlowArgs: flowArgs,
            RequestFqn: requestFqn,
            ResponseFqn: responseFqn,
            HandlerFqn: handlerFqn,
            ValidatorFqn: validatorFqn,
            Valid: valid,
            Location: location);
    }

    private static void Emit(SourceProductionContext spc, ImmutableArray<SliceModel> models)
    {
        var valid = new List<SliceModel>();
        foreach (var model in models)
        {
            if (!model.Valid)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    LinbikSliceConstants.MissingRequestOrHandler, model.Location, model.DisplayName));
                continue;
            }
            valid.Add(model);
        }

        // Hiç geçerli slice yoksa registry üretme. Aksi halde slice içermeyen
        // projeler (ör. Linbik.Slices'ın kendisi) boş bir LinbikSlicesRegistry
        // üretip dll'e gömer; bu, gerçek slice'ları olan tüketici projede
        // üretilen registry ile çakışır / onu gölgeler.
        if (valid.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using Microsoft.AspNetCore.Http;");
        sb.AppendLine("using Microsoft.AspNetCore.Routing;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine($"namespace {LinbikSliceConstants.GeneratedNamespace}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Source generator tarafından üretilen slice kayıt ve mapping'leri.</summary>");
        sb.AppendLine("    public static class LinbikSlicesRegistry");
        sb.AppendLine("    {");

        // AddLinbikSlices
        sb.AppendLine("        /// <summary>ILinbikSender + tüm slice handler/validator kayıtlarını ekler.</summary>");
        sb.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddLinbikSlices(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine("            global::Linbik.Slices.LinbikSlicesServiceCollectionExtensions.AddLinbikSender(services);");
        foreach (var m in valid)
        {
            sb.AppendLine($"            services.AddScoped<global::Linbik.Slices.ILinbikHandler<{m.RequestFqn}, {m.ResponseFqn}>, {m.HandlerFqn}>();");
            if (m.ValidatorFqn is not null)
                sb.AppendLine($"            services.AddScoped<global::Linbik.Slices.ILinbikValidator<{m.RequestFqn}>, {m.ValidatorFqn}>();");
        }
        sb.AppendLine("            return services;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // MapLinbikSlices
        sb.AppendLine("        /// <summary>Tüm slice endpoint'lerini map'ler (flow metadata + filter dahil).</summary>");
        sb.AppendLine("        public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapLinbikSlices(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)");
        sb.AppendLine("        {");
        foreach (var m in valid)
        {
            var mapMethod = MapMethodFor(m.Method);
            sb.AppendLine("            {");
            sb.AppendLine($"                var __e = app.{mapMethod}(\"{Escape(m.Pattern)}\",");
            sb.AppendLine($"                    static ({m.RequestFqn} request, global::Linbik.Slices.ILinbikSender sender, global::System.Threading.CancellationToken ct)");
            sb.AppendLine($"                        => global::Linbik.Slices.Endpoints.LinbikEndpoint.Handle<{m.RequestFqn}, {m.ResponseFqn}>(request, sender, ct));");
            sb.AppendLine("                __e.AddEndpointFilter<global::Linbik.Slices.Endpoints.LinbikFlowEndpointFilter>();");
            sb.AppendLine($"                __e.WithMetadata(new global::Linbik.Core.Attributes.LFlowAuthorizeAttribute({m.FlowArgs}));");
            sb.AppendLine($"                __e.WithTags(\"{Escape(m.Tag)}\");");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            return app;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource("LinbikSlicesRegistry.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string MapMethodFor(string method) => method switch
    {
        "GET" => "MapGet",
        "PUT" => "MapPut",
        "DELETE" => "MapDelete",
        "PATCH" => "MapPatch",
        _ => "MapPost",
    };

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

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

/// <summary>Bir slice'tan çıkarılan, kod üretimi için gereken bilgiler.</summary>
internal sealed record SliceModel(
    string DisplayName,
    string Pattern,
    string Method,
    string Tag,
    string FlowArgs,
    string? RequestFqn,
    string? ResponseFqn,
    string? HandlerFqn,
    string? ValidatorFqn,
    bool Valid,
    Location Location);
