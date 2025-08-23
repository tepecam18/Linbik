namespace Linbik.Core.Interfaces;

public interface ICurrentActor
{
    // Authentication Status
    bool IsAuthenticated { get; }
    
    // User Information
    Guid? UserId { get; }
    string? Username { get; }        // Login için kullanıcı adı (john_doe)
    string? FirstName { get; }       // Kullanıcının adı (John)
    string? LastName { get; }        // Soyadı (Doe)
    
    // App Information
    Guid? AppId { get; }
    
    // System Information
    //string TenantId { get; }
    UserType UserType { get; }
}

public enum UserType
{
    Unknown = 0,
    User = 1,
    App = 2
}
