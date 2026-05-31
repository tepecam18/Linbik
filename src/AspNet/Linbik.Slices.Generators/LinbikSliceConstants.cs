using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Linbik.Slices.Generators;

/// <summary>
/// Generator/analyzer için ortak metadata adları ve tanılama tanımları.
/// </summary>
internal static class LinbikSliceConstants
{
    public const string SliceAttribute = "Linbik.Slices.LinbikSliceAttribute";
    public const string FlowAttribute = "Linbik.Slices.LFlowAttribute";
    public const string FlowPublicAttribute = "Linbik.Slices.LFlowPublicAttribute";

    public const string RequestInterface = "Linbik.Slices.ILinbikRequest`1";
    public const string HandlerInterface = "Linbik.Slices.ILinbikHandler`2";
    public const string ValidatorInterface = "Linbik.Slices.ILinbikValidator`1";

    public const string GeneratedNamespace = "Linbik.Slices.Generated";

    public static readonly DiagnosticDescriptor MissingFlow = new(
        id: "LINBIK001",
        title: "Slice akış (flow) deklarasyonu eksik",
        messageFormat: "'{0}' [LinbikSlice] taşıyor ama [LFlow] veya [LFlowPublic] yok. Deny-by-default: erişilen akışları açıkça bildirin (public için [LFlowPublic]).",
        category: "Linbik.Slices",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Her slice, hangi yetkilendirme akışlarına açık olduğunu açıkça deklare etmelidir. Unutulan attribute kazara public endpoint açmasın diye derleme kırılır.");

    public static readonly DiagnosticDescriptor MissingRequestOrHandler = new(
        id: "LINBIK002",
        title: "Slice Request veya Handler eksik",
        messageFormat: "'{0}' [LinbikSlice] taşıyor ama iç içe bir Request (ILinbikRequest<>) ve/veya Handler (ILinbikHandler<,>) bulunamadı.",
        category: "Linbik.Slices",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Bir slice; ILinbikRequest<TResponse> uygulayan bir Request ve ILinbikHandler<TRequest,TResponse> uygulayan bir Handler içermelidir.");

    public static ImmutableArray<DiagnosticDescriptor> All =>
        ImmutableArray.Create(MissingFlow, MissingRequestOrHandler);
}
