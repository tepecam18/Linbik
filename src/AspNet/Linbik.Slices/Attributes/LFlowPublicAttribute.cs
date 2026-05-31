namespace Linbik.Slices;

/// <summary>
/// Bir slice'ı bilinçli olarak public/anonim işaretler. Deny-by-default
/// (<c>LINBIK001</c>) kuralını karşılar ve OpenAPI'de <c>linbik-flows: ["*"]</c> üretir.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class LFlowPublicAttribute : Attribute;
