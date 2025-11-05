# Linbik.Core

**Simple HTTP Client for Linbik.App Authorization Server**

---

## 🎯 Purpose

`Linbik.Core` provides a lightweight HTTP client for communicating with **Linbik.App** (the OAuth 2.0 authorization server). 

**This library is for CLIENT APPLICATIONS** - apps where users login and use services.

### What It Does:
✅ Builds authorization URLs (redirect users to Linbik login)  
✅ Exchanges authorization codes for tokens  
✅ Refreshes expired tokens  
✅ Gets user profile data  

### What It Does NOT Do:
❌ JWT validation (that's **Linbik.Server**)  
❌ Token generation (only **Linbik.App** does that)  
❌ Cookie/session management (that's **Linbik.JwtAuthManager**)  
❌ API Gateway routing (that's **Linbik.YARP**)  

---

## 📦 Installation

```bash
dotnet add package Linbik.Core
```

---

## 🔧 Configuration

### appsettings.json

```json
{
  "Linbik": {
    "ServerUrl": "https://localhost:5001",
    "ServiceId": "your-service-guid-from-linbik-app",
    "ApiKey": "linbik_your_api_key_from_linbik_app",
    "EnablePKCE": true
  }
}
```

### Program.cs

```csharp
builder.Services.AddLinbikClient(builder.Configuration);
```

---

## 💻 Usage Examples

### 1. Redirect User to Login

```csharp
public class AuthController : Controller
{
    private readonly ILinbikHttpClient _linbikClient;

    public AuthController(ILinbikHttpClient linbikClient)
    {
        _linbikClient = linbikClient;
    }

    [HttpGet("/login")]
    public IActionResult Login()
    {
        var serviceId = Guid.Parse(Configuration["Linbik:ServiceId"]);
        var authUrl = _linbikClient.GetAuthorizationUrl(serviceId);
        
        return Redirect(authUrl);
    }
}
```

### 2. Handle OAuth Callback

```csharp
[HttpGet("/oauth/callback")]
public async Task<IActionResult> Callback([FromQuery] string code)
{
    var tokenResponse = await _linbikClient.ExchangeCodeForTokensAsync(code);
    
    if (tokenResponse == null)
        return BadRequest("Token exchange failed");
    
    // Store in session
    HttpContext.Session.SetString("userId", tokenResponse.UserId.ToString());
    HttpContext.Session.SetString("userName", tokenResponse.UserName);
    HttpContext.Session.SetString("refreshToken", tokenResponse.RefreshToken);
    
    // Store integration tokens
    foreach (var integration in tokenResponse.Integrations)
    {
        HttpContext.Session.SetString(
            $"token_{integration.ServicePackage}",
            integration.Token
        );
    }
    
    return RedirectToAction("Dashboard", "Home");
}
```

---

## 📋 API Reference

### ILinbikHttpClient

```csharp
public interface ILinbikHttpClient
{
    string GetAuthorizationUrl(Guid serviceId, string? codeChallenge = null);
    Task<LinbikTokenResponse?> ExchangeCodeForTokensAsync(string authorizationCode);
    Task<LinbikTokenResponse?> RefreshTokensAsync(string refreshToken);
    Task<UserProfileData?> GetUserProfileAsync(Guid userId, Guid profileId);
}
```

---

## 🏗️ Architecture

```
┌─────────────────────────────────────┐
│   Your Web App (Client)             │
│   - Uses Linbik.JwtAuthManager      │
│   - Uses Linbik.Core (this)         │
└──────────────┬──────────────────────┘
               │
               │ HTTP Requests
               ↓
┌──────────────────────────────────────┐
│   Linbik.App                         │
│   (OAuth 2.0 Authorization Server)   │
└──────────────────────────────────────┘
```

---

## 🔗 Related Libraries

- **Linbik.JwtAuthManager** - Cookie-based user authentication
- **Linbik.Server** - JWT validation for integration services
- **Linbik.YARP** - API Gateway with automatic token injection

---

**Version**: 2.0.0  
**Last Updated**: 1 Kasım 2025
