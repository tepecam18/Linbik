# Linbik.JwtAuthManager

**Cookie-Based User Authentication for Client Applications**

---

## 🎯 Purpose

`Linbik.JwtAuthManager` handles **user login and session management** for your main application. It redirects users to Linbik.App for login and manages their session with cookies.

**This library is for CLIENT APPLICATIONS** - your main web app where users login.

### What It Does:
✅ Configures `options.LoginPath` to redirect to Linbik  
✅ Handles OAuth callback from Linbik.App  
✅ Manages user session with cookies  
✅ Provides `[Authorize]` attribute functionality  

### What It Does NOT Do:
❌ JWT validation (not needed for user sessions)  
❌ Token generation (only Linbik.App does that)  
❌ Integration service authentication (that's **Linbik.Server**)  

---

## 📦 Installation

```bash
dotnet add package Linbik.JwtAuthManager
dotnet add package Linbik.Core
```

---

## 🔧 Configuration

### appsettings.json

```json
{
  "Linbik": {
    "ServerUrl": "https://localhost:5001",
    "ServiceId": "your-service-guid",
    "ApiKey": "linbik_your_api_key"
  }
}
```

### Program.cs

```csharp
using Linbik.Core.Extensions;
using Linbik.JwtAuthManager.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Linbik HTTP client
builder.Services.AddLinbikClient(builder.Configuration);

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Add cookie authentication with Linbik integration
builder.Services.AddLinbikAuthentication(options =>
{
    options.LoginPath = "/login";              // Your login endpoint
    options.CallbackPath = "/oauth/callback";  // OAuth callback endpoint
    options.AccessDeniedPath = "/access-denied";
    options.CookieName = "MyApp.Auth";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## 💻 Usage Examples

### 1. Login Controller

```csharp
using Linbik.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

public class AuthController : Controller
{
    private readonly ILinbikHttpClient _linbikClient;
    private readonly IConfiguration _configuration;

    public AuthController(ILinbikHttpClient linbikClient, IConfiguration configuration)
    {
        _linbikClient = linbikClient;
        _configuration = configuration;
    }

    [HttpGet("/login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        // Store return URL in session
        if (!string.IsNullOrEmpty(returnUrl))
            HttpContext.Session.SetString("LoginReturnUrl", returnUrl);
        
        // Redirect to Linbik for authentication
        var serviceId = Guid.Parse(_configuration["Linbik:ServiceId"]!);
        var authUrl = _linbikClient.GetAuthorizationUrl(serviceId);
        
        return Redirect(authUrl);
    }

    [HttpGet("/oauth/callback")]
    public async Task<IActionResult> Callback([FromQuery] string code)
    {
        try
        {
            // Exchange authorization code for tokens
            var tokenResponse = await _linbikClient.ExchangeCodeForTokensAsync(code);
            
            if (tokenResponse == null)
                return BadRequest("Token exchange failed");
            
            // Create authentication cookie
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, tokenResponse.UserId.ToString()),
                new Claim(ClaimTypes.Name, tokenResponse.UserName),
                new Claim("NickName", tokenResponse.NickName)
            };
            
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });
            
            // Store tokens in session
            HttpContext.Session.SetString("RefreshToken", tokenResponse.RefreshToken);
            
            foreach (var integration in tokenResponse.Integrations)
            {
                HttpContext.Session.SetString(
                    $"IntegrationToken_{integration.ServicePackage}",
                    integration.Token
                );
                HttpContext.Session.SetString(
                    $"IntegrationToken_{integration.ServicePackage}_Expires",
                    integration.ExpiresAt.ToString("o")
                );
            }
            
            // Redirect to original URL or home
            var returnUrl = HttpContext.Session.GetString("LoginReturnUrl") ?? "/";
            HttpContext.Session.Remove("LoginReturnUrl");
            
            return Redirect(returnUrl);
        }
        catch (Exception ex)
        {
            return BadRequest($"Authentication failed: {ex.Message}");
        }
    }

    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        
        return RedirectToAction("Index", "Home");
    }
}
```

### 2. Protected Controller

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public class DashboardController : Controller
{
    [HttpGet("/dashboard")]
    public IActionResult Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.FindFirstValue(ClaimTypes.Name);
        var nickName = User.FindFirst("NickName")?.Value;
        
        return View(new DashboardViewModel
        {
            UserId = Guid.Parse(userId!),
            UserName = userName!,
            NickName = nickName!
        });
    }
}
```

### 3. Integration Service Helper

```csharp
public class IntegrationTokenHelper
{
    private readonly ILinbikHttpClient _linbikClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IntegrationTokenHelper(
        ILinbikHttpClient linbikClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _linbikClient = linbikClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string?> GetValidTokenAsync(string servicePackage)
    {
        var session = _httpContextAccessor.HttpContext!.Session;
        
        var token = session.GetString($"IntegrationToken_{servicePackage}");
        var expiresStr = session.GetString($"IntegrationToken_{servicePackage}_Expires");
        
        // Check if token expired
        if (DateTime.TryParse(expiresStr, out var expiresAt) && 
            expiresAt.AddMinutes(-5) < DateTime.UtcNow)
        {
            // Refresh tokens
            var refreshToken = session.GetString("RefreshToken");
            if (string.IsNullOrEmpty(refreshToken))
                return null;
            
            var tokenResponse = await _linbikClient.RefreshTokensAsync(refreshToken);
            if (tokenResponse == null)
                return null;
            
            // Update session
            session.SetString("RefreshToken", tokenResponse.RefreshToken);
            
            foreach (var integration in tokenResponse.Integrations)
            {
                session.SetString(
                    $"IntegrationToken_{integration.ServicePackage}",
                    integration.Token
                );
                session.SetString(
                    $"IntegrationToken_{integration.ServicePackage}_Expires",
                    integration.ExpiresAt.ToString("o")
                );
            }
            
            token = session.GetString($"IntegrationToken_{servicePackage}");
        }
        
        return token;
    }
}

// Register in Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IntegrationTokenHelper>();
```

### 4. Call Integration Service

```csharp
[Authorize]
public class PaymentController : Controller
{
    private readonly IntegrationTokenHelper _tokenHelper;
    private readonly IHttpClientFactory _httpClientFactory;

    public PaymentController(
        IntegrationTokenHelper tokenHelper,
        IHttpClientFactory httpClientFactory)
    {
        _tokenHelper = tokenHelper;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("/api/payment/charge")]
    public async Task<IActionResult> Charge([FromBody] ChargeRequest request)
    {
        // Get valid token for payment service
        var token = await _tokenHelper.GetValidTokenAsync("payment-gateway");
        if (string.IsNullOrEmpty(token))
            return Unauthorized("Payment service not authorized");
        
        // Call payment service
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var response = await client.PostAsJsonAsync(
            "https://payment-gateway.com/api/charge",
            new { linbik_user_id = userId, amount = request.Amount }
        );
        
        if (!response.IsSuccessStatusCode)
            return BadRequest("Payment failed");
        
        var result = await response.Content.ReadFromJsonAsync<PaymentResult>();
        return Ok(result);
    }
}
```

---

## 🏗️ Architecture

```
┌────────────────────────────────────────┐
│   Your Web App                         │
│   - Linbik.JwtAuthManager (this)      │
│   - Linbik.Core                        │
│   - Cookie authentication              │
│   - Session management                 │
└───────────┬────────────────────────────┘
            │
            │ User login redirect
            │ OAuth callback
            ↓
┌────────────────────────────────────────┐
│   Linbik.App                           │
│   (Authorization Server)               │
└────────────────────────────────────────┘
```

---

## 🔄 Authentication Flow

1. **User visits protected page** → Redirected to `/login`

2. **Login endpoint** → Redirects to Linbik.App
   ```csharp
   var authUrl = _linbikClient.GetAuthorizationUrl(serviceId);
   return Redirect(authUrl);
   ```

3. **User authenticates on Linbik** → Redirected back with code
   ```
   GET /oauth/callback?code=auth_xyz123
   ```

4. **Callback handler** → Exchange code, create cookie, store tokens
   ```csharp
   var tokens = await _linbikClient.ExchangeCodeForTokensAsync(code);
   await HttpContext.SignInAsync(principal, properties);
   ```

5. **User is authenticated** → Cookie valid for 7 days

6. **Access protected pages** → `[Authorize]` works automatically

---

## 📋 Configuration Options

```csharp
public class LinbikAuthenticationOptions
{
    /// <summary>
    /// Path where users are redirected for login
    /// Default: "/login"
    /// </summary>
    public string LoginPath { get; set; } = "/login";
    
    /// <summary>
    /// OAuth callback path from Linbik
    /// Default: "/oauth/callback"
    /// </summary>
    public string CallbackPath { get; set; } = "/oauth/callback";
    
    /// <summary>
    /// Path for access denied page
    /// Default: "/access-denied"
    /// </summary>
    public string AccessDeniedPath { get; set; } = "/access-denied";
    
    /// <summary>
    /// Authentication cookie name
    /// Default: "LinbikAuth"
    /// </summary>
    public string CookieName { get; set; } = "LinbikAuth";
    
    /// <summary>
    /// Cookie expiration time
    /// Default: 7 days
    /// </summary>
    public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromDays(7);
}
```

---

## 🔗 Related Libraries

- **Linbik.Core** - HTTP client for Linbik.App (required)
- **Linbik.Server** - JWT validation for integration services
- **Linbik.YARP** - API Gateway for automatic token injection

---

## 📄 License

See main repository LICENSE

---

**Version**: 2.0.0  
**Purpose**: User authentication and session management  
**For**: Client applications (main web apps)  
**Last Updated**: 1 Kasım 2025
