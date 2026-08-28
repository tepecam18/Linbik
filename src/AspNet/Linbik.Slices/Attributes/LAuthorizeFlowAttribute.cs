namespace Linbik.Slices;

/// <summary>
/// Bir slice'ı, <c>Linbik-Flow</c> header'ına (bkz. <see cref="LFlowAttribute"/>) güvenmek
/// yerine gerçek kimlik doğrulamasına (ilgili PASETO/JWT authentication scheme'i) göre
/// yetkilendirir. API Gateway olmayan mimariler (servis token'ı doğrudan kendisi
/// doğruluyorsa) için kullanın — spoof edilebilir bir header yerine ASP.NET Core'un
/// <c>UseAuthentication</c>/<c>UseAuthorization</c> boru hattı çalışır.
/// </summary>
/// <remarks>
/// Parametresiz <c>[LAuthorizeFlow]</c> = üç akıştan (Self/Delegated/Application) herhangi
/// biriyle authenticate olmuş istek yeterli. Bir slice, <see cref="LFlowAttribute"/>,
/// <see cref="LFlowPublicAttribute"/> ve bu attribute'tan yalnızca birini taşıyabilir.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class LAuthorizeFlowAttribute : Attribute
{
    /// <summary>İzin verilen akışlar.</summary>
    public string[] Flows { get; }

    /// <param name="flows">İzin verilen akışlar. Boş bırakılırsa üç akıştan biri yeterlidir.</param>
    public LAuthorizeFlowAttribute(params LinbikFlow[] flows)
    {
        Flows = flows is null ? [] : Array.ConvertAll(flows, static f => f.ToString());
    }
}
