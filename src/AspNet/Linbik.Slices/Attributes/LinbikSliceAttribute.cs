namespace Linbik.Slices;

/// <summary>
/// Bir tipi vertical-slice olarak işaretler. Source generator bu tip için endpoint
/// mapping'ini ve DI kayıtlarını üretir. İşaretli tip; iç içe bir <c>Request</c>
/// (<see cref="ILinbikRequest{TResponse}"/>), <c>Response</c>, <c>Handler</c>
/// (<see cref="ILinbikHandler{TRequest, TResponse}"/>) ve opsiyonel <c>Validator</c>
/// (<see cref="ILinbikValidator{TRequest}"/>) barındırmalıdır.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class LinbikSliceAttribute : Attribute
{
    /// <summary>Endpoint route şablonu, örn. <c>/api/arithmetic/divide</c>.</summary>
    public string Pattern { get; }

    /// <summary>HTTP metodu (varsayılan <c>POST</c>).</summary>
    public string Method { get; init; } = "POST";

    /// <summary>OpenAPI tag(ler)i. Belirtilmezse slice tipinin adı kullanılır.</summary>
    public string? Tag { get; init; }

    /// <param name="pattern">Endpoint route şablonu.</param>
    public LinbikSliceAttribute(string pattern)
    {
        Pattern = pattern;
    }
}
