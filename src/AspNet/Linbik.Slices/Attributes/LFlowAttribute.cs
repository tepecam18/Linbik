namespace Linbik.Slices;

/// <summary>
/// Bir slice'ın hangi yetkilendirme akış(lar)ına açık olduğunu deklare eder.
/// Source generator bunu endpoint metadata'sındaki
/// <c>LFlowAuthorizeAttribute</c>'a çevirir; OpenAPI <c>linbik-flows</c> uzantısı
/// ve runtime <c>Linbik-Flow</c> doğrulaması buradan beslenir.
/// </summary>
/// <remarks>
/// Parametresiz <c>[LFlow]</c> = herhangi bir authenticated akış yeterli.
/// Public/anonim endpoint için <see cref="LFlowPublicAttribute"/> kullanın.
/// <c>[LinbikSlice]</c> taşıyan ama <c>[LFlow]</c>/<c>[LFlowPublic]</c> taşımayan
/// tipler <c>LINBIK001</c> ile derleme hatası verir (deny-by-default).
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class LFlowAttribute : Attribute
{
    /// <summary>İzin verilen akışlar (örn. <c>LinbikDefaults.Flows.Self</c>).</summary>
    public string[] Flows { get; }

    /// <param name="flows">İzin verilen akışlar. Boş bırakılırsa "authenticated".</param>
    public LFlowAttribute(params string[] flows)
    {
        Flows = flows ?? [];
    }
}
