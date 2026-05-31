namespace Linbik.Slices.Results;

/// <summary>
/// Bir işlemin başarısızlık nedeni. HTTP'ye eşlenebilmesi için
/// <see cref="Status"/> taşır (400/403/404/409 ...).
/// </summary>
/// <param name="Status">Önerilen HTTP durum kodu.</param>
/// <param name="Title">Makinece okunur kısa hata kodu (örn. <c>validation_failed</c>).</param>
/// <param name="Message">Kullanıcıya gösterilebilir açıklama.</param>
/// <param name="Errors">Alan bazlı validasyon hataları (varsa).</param>
public sealed record LError(
    int Status,
    string Title,
    string Message,
    IReadOnlyList<LValidationError>? Errors = null)
{
    /// <summary>400 — geçersiz istek.</summary>
    public static LError Validation(string message, IReadOnlyList<LValidationError>? errors = null)
        => new(400, "validation_failed", message, errors);

    /// <summary>404 — kaynak bulunamadı.</summary>
    public static LError NotFound(string message)
        => new(404, "not_found", message);

    /// <summary>409 — çakışma.</summary>
    public static LError Conflict(string message)
        => new(409, "conflict", message);

    /// <summary>403 — yetki yok.</summary>
    public static LError Forbidden(string message)
        => new(403, "forbidden", message);

    /// <summary>400 — genel istek hatası.</summary>
    public static LError BadRequest(string title, string message)
        => new(400, title, message);
}
