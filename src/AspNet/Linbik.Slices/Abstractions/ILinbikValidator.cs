using Linbik.Slices.Results;

namespace Linbik.Slices;

/// <summary>
/// Bir slice isteği için validasyon kuralı. Pipeline'da handler'dan ÖNCE çalışır;
/// geçersizse handler hiç çağrılmaz.
/// </summary>
public interface ILinbikValidator<TRequest>
{
    /// <summary>İsteği doğrular ve hataları (varsa) döner.</summary>
    ValueTask<LValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken);
}
