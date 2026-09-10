namespace Linbik.Slices.Results;

/// <summary>
/// Yalın, akıcı validasyon kurucusu (FluentValidation bağımlılığı olmadan).
/// Konvansiyon: <c>LValidation.For(request).Ensure(...).BuildAsync()</c>.
/// </summary>
public static class LValidation
{
    /// <summary>Verilen istek için bir validasyon zinciri başlatır.</summary>
    public static Builder<T> For<T>(T request) => new(request);

    /// <summary>Akıcı kural zinciri.</summary>
    public readonly struct Builder<T>
    {
        private readonly T _request;
        private readonly List<LValidationError> _errors;

        internal Builder(T request)
        {
            _request = request;
            _errors = [];
        }

        /// <summary>
        /// <paramref name="predicate"/> false ise <paramref name="field"/>/<paramref name="message"/>
        /// ile bir hata ekler. Zincirleme için kendini döner.
        /// </summary>
        public Builder<T> Ensure(Func<T, bool> predicate, string field, string message)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            if (!predicate(_request))
                _errors.Add(new LValidationError(field, message));
            return this;
        }

        /// <summary>
        /// <paramref name="condition"/> false ise kuralı hiç değerlendirmeden atlar (hata eklemez).
        /// Aktöre/flow'a bağlı koşullu kurallar için (ör. <c>EnsureWhen(actor is LActor.Anonymous, ...)</c>).
        /// </summary>
        public Builder<T> EnsureWhen(bool condition, Func<T, bool> predicate, string field, string message)
            => condition ? Ensure(predicate, field, message) : this;

        /// <summary>Toplanmış hatalardan bir <see cref="LValidationResult"/> üretir.</summary>
        public LValidationResult Build()
            => _errors.Count == 0 ? LValidationResult.Success : LValidationResult.Fail(_errors);

        /// <summary>Senkron kuralları async imzaya uyacak şekilde sarar.</summary>
        public ValueTask<LValidationResult> BuildAsync() => new(Build());
    }
}
