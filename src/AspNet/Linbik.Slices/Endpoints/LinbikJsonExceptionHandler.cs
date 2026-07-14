using Linbik.Core.Responses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Linbik.Slices.Endpoints;

/// <summary>
/// Minimal API'nin JSON gövde bağlama hatalarını (geçersiz GUID, eksik alan, bozuk JSON vb.)
/// yakalayıp platformun standart <see cref="LBaseResponse{T}"/> sözleşmesine çevirir.
/// <para>
/// Sorun: ASP.NET Core, bir <c>[FromBody]</c> parametresini JSON'dan deserialize ederken
/// hata alırsa (ör. <c>System.Text.Json.JsonException</c>), bunu slice pipeline'ı
/// (validator/handler) hiç devreye girmeden — yani bizim <see cref="Result{T}"/> tabanlı
/// hata sözleşmemiz dışında — ham bir <see cref="BadHttpRequestException"/> olarak fırlatır.
/// Bu handler olmadan istemci ASP.NET'in ham geliştirici hata sayfasını/istisna JSON'ını
/// görür; bizim <c>LBaseResponse</c> formatını değil.
/// </para>
/// </summary>
public sealed class LinbikJsonExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // TÜM BadHttpRequestException'ları yakala — yalnız JsonException'ı InnerException
        // olarak taşıyanları değil. "Request body is required" (GET/DELETE gibi body
        // göndermeyen istemcilerde [FromBody] boş gövdeyle karşılaşınca), yanlış
        // Content-Type, gövde boyutu aşımı gibi durumlar JsonException SARMAZ — önceki
        // dar kontrol bu durumları kaçırıp ASP.NET'in varsayılan ProblemDetails/500'üne
        // düşmesine yol açıyordu (bizim LBaseResponse sözleşmemiz dışında).
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        // BadHttpRequestException.StatusCode zaten doğru anlamlı kodu taşır (genelde 400).
        var statusCode = badRequest.StatusCode is >= 400 and < 500
            ? badRequest.StatusCode
            : StatusCodes.Status400BadRequest;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var detail = badRequest.InnerException?.Message ?? badRequest.Message;
        var response = new LBaseResponse<object>(
            title: "INVALID_REQUEST_BODY",
            message: $"Geçersiz istek gövdesi: {detail}",
            isSuccess: false);

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
