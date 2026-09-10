using Microsoft.CodeAnalysis;

namespace Linbik.Slices.Generators;

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
