namespace Linbik.Core.Exceptions;

/// <summary>
/// Base exception class for all Linbik-related exceptions.
/// </summary>
/// <remarks>
/// This exception serves as the root of the Linbik exception hierarchy.
/// Catch this exception to handle all Linbik-specific errors.
/// </remarks>
public class LinbikException : Exception
{
    /// <summary>
    /// Gets the error code associated with this exception.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikException"/> class.
    /// </summary>
    public LinbikException()
        : base("A Linbik error occurred.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public LinbikException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikException"/> class with a specified error message and error code.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="errorCode">The error code for programmatic handling.</param>
    public LinbikException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LinbikException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikException"/> class with a specified error message,
    /// error code, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="errorCode">The error code for programmatic handling.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LinbikException(string message, string errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
