namespace Linbik.Slices.Results;

/// <summary>
/// Bir işlemin başarı/başarısızlık zarfı. Handler'lar istisna fırlatmak yerine
/// bu tipi döner; endpoint katmanı <c>ToHttp()</c> ile <c>LBaseResponse</c>'a çevirir.
/// </summary>
public readonly struct Result<T>
{
    private Result(bool isSuccess, T? value, LError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>İşlem başarılı mı?</summary>
    public bool IsSuccess { get; }

    /// <summary>Başarısızsa <c>false</c>.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Başarılıysa sonuç değeri.</summary>
    public T? Value { get; }

    /// <summary>Başarısızsa hata bilgisi.</summary>
    public LError? Error { get; }

    /// <summary>Başarılı sonuç oluşturur.</summary>
    public static Result<T> Ok(T value) => new(true, value, null);

    /// <summary>Başarısız sonuç oluşturur.</summary>
    public static Result<T> Fail(LError error) => new(false, default, error);

    /// <summary><typeparamref name="T"/>'den örtük başarı dönüşümü.</summary>
    public static implicit operator Result<T>(T value) => Ok(value);

    /// <summary><see cref="LError"/>'den örtük başarısızlık dönüşümü.</summary>
    public static implicit operator Result<T>(LError error) => Fail(error);

    /// <summary>Sonucu tamamlanmış bir <see cref="ValueTask{TResult}"/> içine sarar.</summary>
    public ValueTask<Result<T>> AsValueTask() => new(this);
}

/// <summary>
/// <see cref="Result{T}"/> fabrikaları için kısa yollar.
/// </summary>
public static class Result
{
    /// <summary>Başarılı sonuç.</summary>
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);

    /// <summary>Başarısız sonuç.</summary>
    public static Result<T> Fail<T>(LError error) => Result<T>.Fail(error);
}
