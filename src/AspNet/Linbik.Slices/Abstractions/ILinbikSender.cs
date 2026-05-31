using Linbik.Slices.Results;

namespace Linbik.Slices;

/// <summary>
/// Slice isteklerini ilgili validator + handler pipeline'ına yönlendiren hafif
/// dispatcher. MediatR benzeri ama sıfır dış bağımlılık (K1).
/// </summary>
public interface ILinbikSender
{
    /// <summary>
    /// İsteği çözümlenmiş validator (varsa) ve handler üzerinden çalıştırır.
    /// Validasyon başarısızsa handler çağrılmaz; <see cref="Result{T}.Fail"/> döner.
    /// </summary>
    ValueTask<Result<TResponse>> Send<TResponse>(
        ILinbikRequest<TResponse> request, CancellationToken cancellationToken = default);
}
