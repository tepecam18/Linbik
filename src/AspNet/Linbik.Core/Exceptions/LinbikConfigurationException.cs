namespace Linbik.Core.Exceptions;

/// <summary>
/// Exception thrown when Linbik configuration is invalid or missing.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown during application startup when:
/// <list type="bullet">
///   <item><description>Required configuration values are missing</description></item>
///   <item><description>Configuration values have invalid format</description></item>
///   <item><description>URLs are malformed</description></item>
///   <item><description>Token lifetimes are out of valid range</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In Program.cs
/// try
/// {
///     builder.Services.AddLinbik(builder.Configuration);
/// }
/// catch (LinbikConfigurationException ex)
/// {
///     Console.WriteLine($"Configuration error: {ex.ConfigurationKey} - {ex.Message}");
///     throw;
/// }
/// </code>
/// </example>
public class LinbikConfigurationException : LinbikException
{
    /// <summary>
    /// Gets the configuration key that caused the exception.
    /// </summary>
    public string? ConfigurationKey { get; }

    /// <summary>
    /// Gets the invalid value that was provided, if any.
    /// </summary>
    public object? InvalidValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikConfigurationException"/> class.
    /// </summary>
    public LinbikConfigurationException()
        : base("Linbik configuration is invalid.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikConfigurationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public LinbikConfigurationException(string message)
        : base(message, "configuration_error")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikConfigurationException"/> class
    /// with a specified configuration key and error message.
    /// </summary>
    /// <param name="configurationKey">The configuration key that is invalid.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public LinbikConfigurationException(string configurationKey, string message)
        : base(message, "configuration_error")
    {
        ConfigurationKey = configurationKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikConfigurationException"/> class
    /// with a specified configuration key, invalid value, and error message.
    /// </summary>
    /// <param name="configurationKey">The configuration key that is invalid.</param>
    /// <param name="invalidValue">The invalid value that was provided.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public LinbikConfigurationException(string configurationKey, object? invalidValue, string message)
        : base(message, "configuration_error")
    {
        ConfigurationKey = configurationKey;
        InvalidValue = invalidValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinbikConfigurationException"/> class
    /// with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LinbikConfigurationException(string message, Exception innerException)
        : base(message, "configuration_error", innerException)
    {
    }
}
