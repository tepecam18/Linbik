using Linbik.Core.Identity;
using Linbik.Slices.Results;

namespace Linbik.Slices;

/// <summary>
/// Bir slice isteği için validasyon kuralı. Pipeline'da handler'dan ÖNCE çalışır;
/// geçersizse handler hiç çağrılmaz.
/// </summary>
public interface ILinbikValidator<TRequest>
{
    /// <summary>
    /// İsteği, çağıranın kimliğine (<see cref="LActor"/>) duyarlı şekilde doğrular. Flow'a özel
    /// kurallar (ör. bir alanın yalnız anonim/Application/Self çağıranlar için anlamlı olması)
    /// burada override edilir. Override edilmezse <see cref="ValidateAsync(TRequest, CancellationToken)"/>'a
    /// yönlenir — mevcut (aktörden habersiz) validator'lar değişiklik gerektirmeden çalışmaya devam eder.
    /// </summary>
    ValueTask<LValidationResult> ValidateAsync(TRequest request, LActor actor, CancellationToken cancellationToken)
        => ValidateAsync(request, cancellationToken);

    /// <summary>İsteği, aktörden bağımsız olarak doğrular ve hataları (varsa) döner.</summary>
    ValueTask<LValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException(
            $"{GetType().Name} must override either ValidateAsync(TRequest, LActor, CancellationToken) or ValidateAsync(TRequest, CancellationToken).");
}
