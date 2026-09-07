using Linbik.Core.Responses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Linbik.Slices.Endpoints;

/// <summary>
/// Slice pipeline'ının (validator/handler) hiç devreye girmediği ya da <see cref="Result{T}"/>
/// akışının dışında kalan TÜM yakalanmamış istisnaları platformun standart
/// <see cref="LBaseResponse{T}"/> sözleşmesine çevirir.
/// <para>
/// İki kategori: (1) <see cref="BadHttpRequestException"/> — ASP.NET Core'un bir
/// <c>[FromBody]</c> parametresini JSON'dan deserialize ederken attığı hatalar (geçersiz GUID,
/// eksik alan, bozuk JSON, boş gövde vb.) — anlamlı 4xx koduyla birlikte gelir. (2) Diğer her
/// istisna — örn. altyapı hataları (RabbitMQ bağlantısı, DB timeout) — genel 500 olarak
/// döndürülür. Bu handler olmadan istemci ASP.NET'in ham <c>ProblemDetails</c>/geliştirici hata
/// sayfasını görür; bizim <c>LBaseResponse</c> formatını değil.
/// </para>
/// </summary>
public sealed class LinbikJsonExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        httpContext.Response.ContentType = "application/json";

        if (exception is BadHttpRequestException badRequest)
        {
            // BadHttpRequestException.StatusCode zaten doğru anlamlı kodu taşır (genelde 400).
            httpContext.Response.StatusCode = badRequest.StatusCode is >= 400 and < 500
                ? badRequest.StatusCode
                : StatusCodes.Status400BadRequest;

            var detail = badRequest.InnerException?.Message ?? badRequest.Message;
            var badRequestResponse = new LBaseResponse<object>(
                title: "INVALID_REQUEST_BODY",
                message: $"Geçersiz istek gövdesi: {detail}",
                isSuccess: false);

            await httpContext.Response.WriteAsJsonAsync(badRequestResponse, cancellationToken);
            return true;
        }

        // Handler/validator dışında (Result<T>/LError akışına hiç girmeyen) yakalanmamış
        // her istisna — örn. altyapı hataları (RabbitMQ bağlantısı, DB timeout) — burada
        // biter. Bu handler olmadan ASP.NET'in ham ProblemDetails/500'ü client'a sızar
        // (bizim LBaseResponse sözleşmemiz dışında bir şekil). Ayrıntılar (stack trace,
        // connection string vb.) güvenlik nedeniyle client'a yansıtılmaz; loglama zaten
        // ExceptionHandlerMiddleware tarafından yapılır.
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var response = new LBaseResponse<object>(
            title: "INTERNAL_SERVER_ERROR",
            message: "Beklenmeyen bir hata oluştu.",
            isSuccess: false);

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
