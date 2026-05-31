namespace Linbik.Slices.Results;

/// <summary>
/// Tek bir alanın validasyon hatası.
/// </summary>
/// <param name="Field">Hatalı alanın adı.</param>
/// <param name="Message">İnsan-okur hata mesajı.</param>
public sealed record LValidationError(string Field, string Message);

/// <summary>
/// Bir validasyon çalıştırmasının sonucu. <see cref="IsValid"/> true ise
/// <see cref="Errors"/> boştur.
/// </summary>
public sealed record LValidationResult(IReadOnlyList<LValidationError> Errors)
{
    /// <summary>Hiç hata yoksa geçerlidir.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Hatasız (geçerli) sonuç.</summary>
    public static LValidationResult Success { get; } = new([]);

    /// <summary>Verilen hatalarla başarısız sonuç.</summary>
    public static LValidationResult Fail(IReadOnlyList<LValidationError> errors) => new(errors);
}
