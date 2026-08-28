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
        var assemblyName = context.CompilationProvider.Select(static (c, _) => c.AssemblyName);
        var combined = collected.Combine(assemblyName);

        context.RegisterSourceOutput(combined, static (spc, pair) => Emit(spc, pair.Left, pair.Right));
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
        else if (authorizeFlowAttr is not null)
        {
            // [LAuthorizeFlow] Linbik-Flow header'ına güvenmez: LinbikAuthorizeFlowEndpointFilter
            // izin verilen her akışın gerçek authentication scheme'ini dener. flowArgs yine de
            // linbik-flows OpenAPI uzantısı için üretilir.
            var flows = ExtractFlowNames(authorizeFlowAttr);
            flowArgs = flows.Length == 0
                ? string.Empty
                : string.Join(", ", flows.Select(f => "\"" + f.Replace("\"", "\\\"") + "\""));
        }
        else if (flowAttr is not null)
        {
            // LFlowAttribute artık params LinbikFlow[] alıyor (string yerine enum);
            // TypedConstant.Value burada enum'ın alttaki sayısal değeridir, isim değil —
            // ismi bulmak için enum tipinin field'ları üzerinde eşleştirme yapıyoruz.
            var flows = ExtractFlowNames(flowAttr);
            flowArgs = flows.Length == 0
                ? string.Empty
                : string.Join(", ", flows.Select(f => "\"" + f.Replace("\"", "\\\"") + "\""));
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

    private static void Emit(SourceProductionContext spc, ImmutableArray<SliceModel> models, string? assemblyName)
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
        {
            // Linbik.Slices kütüphanesinin kendisi hiçbir zaman slice içermez;
            // bu beklenen durum için gereksiz bir uyarı üretme.
            if (assemblyName != "Linbik.Slices")
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    LinbikSliceConstants.NoSlicesFound, Location.None, assemblyName ?? "?"));
            }
            return;
        }

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
            // GET/DELETE/HEAD gövdesiz (çoğu istemci — taray\u0131c\u0131 fetch/XHR dahil —
            // bu metotlara body koymay\u0131 client-side reddeder): [AsParameters] ile
            // Request'in property'leri query string'ten ba\u011flan\u0131r (\u00f6r. ?threadId=..&page=1).
            // POST/PUT/PATCH: [FromBody] ile JSON g\u00f6vdeden okunur (konvansiyon).
            // Request tipleri (t\u00fcm slice'larda) d\u00fcz alanlardan olu\u015ftu\u011fu i\u00e7in
            // [AsParameters] ile tam uyumlu (Guid/string/int?/bool vb.).
            var isBodylessVerb = m.Method is "GET" or "DELETE" or "HEAD";
            var bindingAttr = isBodylessVerb
                ? "[global::Microsoft.AspNetCore.Http.AsParameters]"
                : "[global::Microsoft.AspNetCore.Mvc.FromBody]";
            sb.AppendLine($"                    static ({bindingAttr} {m.RequestFqn} request, global::Linbik.Slices.ILinbikSender sender, global::System.Threading.CancellationToken ct)");
            sb.AppendLine($"                        => global::Linbik.Slices.Endpoints.LinbikEndpoint.Handle<{m.RequestFqn}, {m.ResponseFqn}>(request, sender, ct));");
            if (m.IsAuthorizeFlow)
            {
                // [LAuthorizeFlow]: Linbik-Flow header'ına güvenmek yerine gerçek
                // authentication scheme'ini dener — Gateway olmadan da güvenli.
                sb.AppendLine("                __e.AddEndpointFilter<global::Linbik.Slices.Endpoints.LinbikAuthorizeFlowEndpointFilter>();");
            }
            else
            {
                sb.AppendLine("                __e.AddEndpointFilter<global::Linbik.Slices.Endpoints.LinbikFlowEndpointFilter>();");
            }
            sb.AppendLine($"                __e.WithMetadata(new global::Linbik.Core.Attributes.LFlowAuthorizeAttribute({m.FlowArgs}));");
            sb.AppendLine($"                __e.WithTags(\"{Escape(m.Tag)}\");");
            // OperationId = slice sınıf adı (ör. "CreateComment"). Bu olmadan ASP.NET'in
            // varsayılan operationId üretimi path+method'tan türetilen uzun/okunaksız bir
            // isim kullanır — NSwag gibi istemci üreteçleri operationId'yi metot adı olarak
            // kullanır, bu yüzden temiz metot isimleri (ör. client.CreateCommentAsync()) için şart.
            sb.AppendLine($"                __e.WithName(\"{Escape(m.DisplayName)}\");");
            if (m.Summary is not null)
                sb.AppendLine($"                __e.WithSummary(\"{Escape(m.Summary)}\");");
            if (m.Description is not null)
                sb.AppendLine($"                __e.WithDescription(\"{Escape(m.Description)}\");");
            // LinbikEndpoint.Handle tip-silinmi\u015f Task<IResult> d\u00f6nd\u00fcr\u00fcr; OpenAPI generator'\u0131
            // bundan y\u00fck (response) \u015femas\u0131n\u0131 \u00e7\u0131karamaz (docs'ta yaln\u0131z "200 OK" g\u00f6r\u00fcn\u00fcr,
            // \u015fema yok). .Produces<T>() ile ba\u015far\u0131 + bilinen hata durum kodlar\u0131n\u0131n
            // hepsinin ayn\u0131 LBaseResponse<TResponse> zarf\u0131n\u0131 kulland\u0131\u011f\u0131n\u0131 aç\u0131k\u00e7a bildiriyoruz
            // (LError: 400/403/404/409 — bkz. Linbik.Slices.Results.LError).
            sb.AppendLine($"                __e.Produces<global::Linbik.Core.Responses.LBaseResponse<{m.ResponseFqn}>>(200);");
            sb.AppendLine($"                __e.Produces<global::Linbik.Core.Responses.LBaseResponse<{m.ResponseFqn}>>(400);");
            sb.AppendLine($"                __e.Produces<global::Linbik.Core.Responses.LBaseResponse<{m.ResponseFqn}>>(403);");
            sb.AppendLine($"                __e.Produces<global::Linbik.Core.Responses.LBaseResponse<{m.ResponseFqn}>>(404);");
            sb.AppendLine($"                __e.Produces<global::Linbik.Core.Responses.LBaseResponse<{m.ResponseFqn}>>(409);");
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

/// <summary>Bir slice'tan çıkarılan, kod üretimi için gereken bilgiler.</summary>
internal sealed record SliceModel(
    string DisplayName,
    string Pattern,
    string Method,
    string Tag,
    string? Summary,
    string? Description,
    string FlowArgs,
    bool IsAuthorizeFlow,
    string? RequestFqn,
    string? ResponseFqn,
    string? HandlerFqn,
    string? ValidatorFqn,
    bool Valid,
    Location Location);
