using Linbik.Slices.Results;

namespace Linbik.Slices;

/// <summary>
/// Bir slice isteğini işleyen iş mantığı. Handler saf kalır: yan etki dışı
/// validasyon <see cref="ILinbikValidator{TRequest}"/> tarafından önceden
/// çalıştırılır, sonuç <see cref="Result{T}"/> olarak döner.
/// </summary>
public interface ILinbikHandler<TRequest, TResponse>
    where TRequest : ILinbikRequest<TResponse>
{
    /// <summary>İsteği işler ve başarı/başarısızlık zarfı döner.</summary>
    ValueTask<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
