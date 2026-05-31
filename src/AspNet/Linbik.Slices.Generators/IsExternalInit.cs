// netstandard2.0 hedefinde record/init desteği için gerekli polyfill.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit;
}
