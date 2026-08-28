namespace Linbik.Slices;

/// <summary>
/// Bilinen yetkilendirme akışları. <see cref="LFlowAttribute"/> ile kullanılır;
/// isimler <c>Linbik-Flow</c> header değerleriyle (bkz. <c>LinbikDefaults.Flows</c>)
/// bire bir eşleşir.
/// </summary>
public enum LinbikFlow
{
    /// <summary>Self / cookie tabanlı kullanıcı oturumu (ClientScheme).</summary>
    Self,

    /// <summary>Başka bir uygulama, son kullanıcı adına çağırıyor (DelegatedScheme).</summary>
    Delegated,

    /// <summary>Service-to-service / client_credentials (ApplicationScheme).</summary>
    Application,
}
