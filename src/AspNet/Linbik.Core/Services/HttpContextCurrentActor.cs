using Linbik.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Linbik.Core.Services;

public class HttpContextCurrentActor : ICurrentActor
{
    private const string UserGuidClaimType = "user_id";
    private const string UsernameClaimType = "username";
    private const string FirstNameClaimType = "first_name";
    private const string LastNameClaimType = "last_name";
    private const string AppIdClaimType = "app_id";
    private const string UserTypeClaimType = "user_type";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentActor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var UserIdStr = _httpContextAccessor.HttpContext?.User?.FindFirst(UserGuidClaimType)?.Value;
            return Guid.TryParse(UserIdStr, out var userGuid) ? userGuid : null;
        }
    }

    public string? Username => _httpContextAccessor.HttpContext?.User?.FindFirst(UsernameClaimType)?.Value;

    public string? FirstName => _httpContextAccessor.HttpContext?.User?.FindFirst(FirstNameClaimType)?.Value;

    public string? LastName => _httpContextAccessor.HttpContext?.User?.FindFirst(LastNameClaimType)?.Value;

    public Guid? AppId
    {
        get
        {
            var appIdStr = _httpContextAccessor.HttpContext?.User?.FindFirst(AppIdClaimType)?.Value;
            return Guid.TryParse(appIdStr, out var appId) ? appId : null;
        }
    }

    public UserType UserType
    {
        get
        {
            var userTypeStr = _httpContextAccessor.HttpContext?.User?.FindFirst(UserTypeClaimType)?.Value;
            return Enum.TryParse<UserType>(userTypeStr, out var userType) ? userType : UserType.Unknown;
        }
    }

    // Computed Properties (otomatik hesaplanır)
    public bool IsUser => UserType == UserType.User;
    public bool IsApp => UserType == UserType.App;
    
    public string? DisplayName
    {
        get
        {
            if (IsUser)
            {
                if (!string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName))
                    return $"{FirstName} {LastName}";
                if (!string.IsNullOrEmpty(FirstName))
                    return FirstName;
                if (!string.IsNullOrEmpty(LastName))
                    return LastName;
                if (!string.IsNullOrEmpty(Username))
                    return Username;
                return "Unknown User";
            }
            else if (IsApp)
            {
                return $"App {AppId}";
            }
            return "Unknown";
        }
    }
}
