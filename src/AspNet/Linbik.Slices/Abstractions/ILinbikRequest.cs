namespace Linbik.Slices;

/// <summary>
/// Bir slice isteğini işaretler. <typeparamref name="TResponse"/> bu isteğin
/// üreteceği başarı yükünün tipidir. <see cref="ILinbikSender"/> ve handler/validator
/// eşleştirmesi bu tip parametresi üzerinden yapılır.
/// </summary>
/// <typeparam name="TResponse">Başarılı işlem sonucunda dönen veri tipi.</typeparam>
public interface ILinbikRequest<TResponse>;
