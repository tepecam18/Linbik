namespace Linbik.Core.Exceptions;

/// <summary>
/// Exception thrown when token operations fail.
/// </summary>
/// <remarks>
/// <para>
/// This exception covers various token-related failures:
/// <list type="bullet">
///   <item><description>Token refresh failures</description></item>
///   <item><description>Token validation failures</description></item>
///   <item><description>Expired tokens</description></item>
///   <item><description>Revoked tokens</description></item>
///   <item><description>Invalid token format</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     var success = await authService.RefreshTokensAsync(context);
/// }
/// catch (LinbikTokenException ex) when (ex.ErrorCode == LinbikTokenException.TokenRevokedError)
/// {
///     // Token was revoked, user needs to re-authenticate
///     return RedirectToAction("Login");
/// }
/// </code>
/// </example>
public class LinbikTokenException : LinbikException
{
    /// <summary>
    /// Error code for expired token.
    /// </summary>
    public const string TokenExpiredError = "token_expired";

    /// <summary>
    /// Error code for revoked token.
    /// </summary>
    public const string TokenRevokedError = "token_revoked";

    /// <summary>
    /// Error code for invalid token format.
    /// </summary>
    public const string InvalidTokenFormatError = "invalid_token_format";

    /// <summary>
    /// Error code for token refresh failure.
    /// </summary>
    public const string RefreshFailedError = "refresh_failed";

    /// <summary>
    /// Error code for token validation failure.
    /// </summary>
    public const string ValidationFailedError = "validation_failed";

    /// <summary>
    /// Gets the type of token that caused the exception.
    /// </summary>
    public string? TokenType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikTokenException"/> class.
    /// </summary>
    public LinbikTokenException()
        : base("Token operation failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikTokenException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public LinbikTokenException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikTokenException"/> class with a specified error message and error code.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="errorCode">The error code for programmatic handling.</param>
    public LinbikTokenException(string message, string errorCode)
        : base(message, errorCode)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikTokenException"/> class
    /// with a specified error message, error code, and token type.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="errorCode">The error code for programmatic handling.</param>
    /// <param name="tokenType">The type of token (e.g., "access", "refresh", "integration").</param>
    public LinbikTokenException(string message, string errorCode, string tokenType)
        : base(message, errorCode)
    {
        TokenType = tokenType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikTokenException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LinbikTokenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikTokenException"/> class with a specified error message,
    /// error code, and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="errorCode">The error code for programmatic handling.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LinbikTokenException(string message, string errorCode, Exception innerException)
        : base(message, errorCode, innerException)
    {
    }
}
