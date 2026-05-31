using Linbik.Core.Responses;
using Linbik.Slices.Results;
using Microsoft.AspNetCore.Http;

namespace Linbik.Slices.Endpoints;

/// <summary>
/// Minimal API endpoint'lerinin slice pipeline'ına bağlanması için yardımcılar.
/// Source generator'ın ürettiği endpoint mapping'leri bu metotları çağırır.
/// </summary>
public static class LinbikEndpoint
{
    /// <summary>
    /// Bir isteği <see cref="ILinbikSender"/> üzerinden çalıştırır ve sonucu
    /// <see cref="LBaseResponse{T}"/> zarfına çeviren bir <see cref="IResult"/> döner.
    /// </summary>
    public static async Task<IResult> Handle<TRequest, TResponse>(
        TRequest request, ILinbikSender sender, CancellationToken cancellationToken)
        where TRequest : ILinbikRequest<TResponse>
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(sender);
        var result = await sender.Send<TResponse>(request, cancellationToken);
        return result.ToHttp();
    }

    /// <summary>
    /// <see cref="Result{T}"/>'i mevcut <see cref="LBaseResponse{T}"/> sözleşmesine
    /// eşler. Başarı → 200 + Data; başarısızlık → hata durum kodu + FriendlyMessage.
    /// </summary>
    public static IResult ToHttp<T>(this Result<T> result) where T : class
    {
        if (result.IsSuccess)
            return Microsoft.AspNetCore.Http.Results.Ok(new LBaseResponse<T>(result.Value!));

        var error = result.Error!;
        return Microsoft.AspNetCore.Http.Results.Json(
            new LBaseResponse<T>(title: error.Title, message: error.Message, isSuccess: false),
            statusCode: error.Status);
    }
}
