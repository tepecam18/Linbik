namespace Linbik.Core.Builders.Interfaces;

/// <summary>
/// Validates that a Linbik module is properly configured at startup.
/// Each Linbik package (Core, JwtAuth, Server, YARP) auto-registers its own validator
/// during <c>AddLinbik*()</c> calls. Call <c>app.EnsureLinbik()</c> to run all validators.
/// </summary>
public interface ILinbikStartupValidator
{
    /// <summary>
    /// Module name for error messages (e.g., "Linbik.Core", "Linbik.Server")
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Validation order. Lower values run first.
    /// Core=0, JwtAuth=10, Server=20, YARP=30
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Validate service registrations and configuration at startup.
    /// Implementations should resolve <c>IOptions&lt;T&gt;.Value</c> to trigger eager options validation,
    /// and verify that critical services are registered.
    /// </summary>
    /// <param name="services">The application's service provider.</param>
    void Validate(IServiceProvider services);
}
