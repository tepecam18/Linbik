namespace Linbik.Core.Exceptions;

/// <summary>
/// Exception thrown when authentication with Linbik fails.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown in scenarios such as:
/// <list type="bullet">
///   <item><description>Invalid or expired authorization code</description></item>
///   <item><description>Invalid API key</description></item>
///   <item><description>Token exchange failure</description></item>
///   <item><description>Invalid credentials</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     var tokens = await authService.ExchangeCodeForTokensAsync(code);
/// }
/// catch (LinbikAuthenticationException ex) when (ex.ErrorCode == "invalid_code")
/// {
///     // Handle expired or invalid authorization code
///     return RedirectToAction("Login");
/// }
/// </code>
/// </example>
public class LinbikAuthenticationException : LinbikException
{
    /// <summary>
    /// Error code for invalid authorization code.
    /// </summary>
    public const string InvalidCodeError = "invalid_code";

    /// <summary>
    /// Error code for expired authorization code.
    /// </summary>
    public const string ExpiredCodeError = "expired_code";

    /// <summary>
    /// Error code for invalid API key.
    /// </summary>
    public const string InvalidApiKeyError = "invalid_api_key";

    /// <summary>
    /// Error code for invalid credentials.
    /// </summary>
    public const string InvalidCredentialsError = "invalid_credentials";

    /// <summary>
    /// Error code for token exchange failure.
    /// </summary>
    public const string TokenExchangeFailedError = "token_exchange_failed";

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikAuthenticationException"/> class.
    /// </summary>
    public LinbikAuthenticationException()
        : base("Authentication failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikAuthenticationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public LinbikAuthenticationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikAuthenticationException"/> class with a specified error message and error code.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="errorCode">The error code for programmatic handling.</param>
    public LinbikAuthenticationException(string message, string errorCode)
        : base(message, errorCode)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikAuthenticationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LinbikAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikAuthenticationException"/> class with a specified error message,
    /// error code, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="errorCode">The error code for programmatic handling.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LinbikAuthenticationException(string message, string errorCode, Exception innerException)
        : base(message, errorCode, innerException)
    {
    }
}
