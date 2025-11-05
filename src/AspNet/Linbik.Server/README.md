# Linbik.Server

**JWT Validation for Integration Services**

---

## 🎯 Purpose

`Linbik.Server` provides **JWT validation** for integration services (payment gateways, courier services, survey tools, etc.). These services receive JWT tokens from main applications and need to validate them.

**This library is for INTEGRATION SERVICES** - shared services used by multiple applications.

### What It Does:
✅ Validates JWT tokens with RSA public key  
✅ Extracts user information from JWT claims  
✅ Verifies token signature, expiration, issuer, audience  

### What It Does NOT Do:
❌ Generate JWT tokens (only **Linbik.App** does that)  
❌ User authentication (that's for **client apps** with **Linbik.JwtAuthManager**)  
❌ HTTP client for Linbik.App (that's **Linbik.Core**)  

---

## 📦 Installation

```bash
dotnet add package Linbik.Server
```

---

## 🔧 Configuration

### appsettings.json

```json
{
  "LinbikServer": {
    "PublicKey": "-----BEGIN PUBLIC KEY-----\nMIIBIjAN...\n-----END PUBLIC KEY-----",
    "Issuer": "Linbik",
    "Audience": "payment-gateway",  // Your service package name
    "ValidateLifetime": true,
    "ClockSkewMinutes": 1
  }
}
```

### Get Your Public Key

1. Register your service in **Linbik.App** as an **Integration Service**
2. Go to `/Service/Edit/{your-service-id}`
3. Copy the **Public Key** (PEM format)
4. Add to appsettings.json

### Program.cs

```csharp
using Linbik.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add JWT authentication with Linbik public key
builder.Services.AddLinbikJwtAuthentication(builder.Configuration);

builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## 💻 Usage Examples

### 1. Protected API Endpoint

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[Authorize]
[ApiController]
[Route("api")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("charge")]
    public async Task<IActionResult> Charge([FromBody] ChargeRequest request)
    {
        // Extract user info from JWT claims
        var userId = User.FindFirstValue("userId");
        var userName = User.FindFirstValue("userName");
        var nickName = User.FindFirstValue("nickName");
        
        if (string.IsNullOrEmpty(userId))
            return Unauthorized("Invalid token");
        
        // Find user's payment method in YOUR database
        var paymentMethod = await _paymentService.GetUserPaymentMethodAsync(
            Guid.Parse(userId)
        );
        
        if (paymentMethod == null)
            return NotFound("No payment method found for this user");
        
        // Process payment
        var result = await _paymentService.ChargeAsync(
            paymentMethod,
            request.Amount
        );
        
        return Ok(result);
    }
}
```

### 2. Database Model (Example)

```csharp
// In YOUR service's database
public class UserPaymentMethod
{
    public Guid Id { get; set; }
    
    // This is the key: Linbik user ID
    public Guid LinbikUserId { get; set; }
    
    // Your service's data
    public string CardToken { get; set; }
    public string Last4 { get; set; }
    public string CardBrand { get; set; }
    public DateTime CreatedAt { get; set; }
}

// When user adds payment method first time:
[Authorize]
[HttpPost("api/payment-method")]
public async Task<IActionResult> AddPaymentMethod([FromBody] AddPaymentMethodRequest request)
{
    var linbikUserId = User.FindFirstValue("userId");
    
    // Store payment method with Linbik user ID
    var paymentMethod = new UserPaymentMethod
    {
        Id = Guid.NewGuid(),
        LinbikUserId = Guid.Parse(linbikUserId),
        CardToken = request.CardToken,
        Last4 = request.Last4,
        CardBrand = request.CardBrand,
        CreatedAt = DateTime.UtcNow
    };
    
    await _db.UserPaymentMethods.AddAsync(paymentMethod);
    await _db.SaveChangesAsync();
    
    return Ok(paymentMethod);
}
```

### 3. User Claims Extraction

```csharp
public class UserClaimsService
{
    public LinbikUserInfo GetUserInfoFromClaims(ClaimsPrincipal user)
    {
        return new LinbikUserInfo
        {
            UserId = Guid.Parse(user.FindFirstValue("userId")!),
            UserName = user.FindFirstValue("userName")!,
            NickName = user.FindFirstValue("nickName")!,
            // JWT standard claims
            Issuer = user.FindFirstValue("iss"),
            Subject = user.FindFirstValue("sub"),  // Main service ID
            Audience = user.FindFirstValue("aud"),  // Your service ID
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(
                long.Parse(user.FindFirstValue("exp")!)
            ).DateTime
        };
    }
}

public class LinbikUserInfo
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string NickName { get; set; }
    public string? Issuer { get; set; }
    public string? Subject { get; set; }
    public string? Audience { get; set; }
    public DateTime ExpiresAt { get; set; }
}
```

### 4. Multiple Services Example

```csharp
// Courier Service
[Authorize]
[HttpPost("api/shipment")]
public async Task<IActionResult> CreateShipment([FromBody] ShipmentRequest request)
{
    var linbikUserId = Guid.Parse(User.FindFirstValue("userId")!);
    
    // Find user's address in YOUR database
    var address = await _db.UserAddresses
        .FirstOrDefaultAsync(a => a.LinbikUserId == linbikUserId);
    
    if (address == null)
        return NotFound("No address found");
    
    var shipment = await _courierService.CreateShipmentAsync(
        address,
        request.Items
    );
    
    return Ok(shipment);
}

// Survey Service
[Authorize]
[HttpPost("api/survey/response")]
public async Task<IActionResult> SubmitResponse([FromBody] SurveyResponse response)
{
    var linbikUserId = Guid.Parse(User.FindFirstValue("userId")!);
    var userName = User.FindFirstValue("userName")!;
    
    // Store survey response with Linbik user info
    var surveyResponse = new SurveyResponseEntity
    {
        Id = Guid.NewGuid(),
        LinbikUserId = linbikUserId,
        LinbikUserName = userName,
        SurveyId = response.SurveyId,
        Answers = response.Answers,
        SubmittedAt = DateTime.UtcNow
    };
    
    await _db.SurveyResponses.AddAsync(surveyResponse);
    await _db.SaveChangesAsync();
    
    return Ok(surveyResponse);
}
```

---

## 🏗️ Architecture

```
┌────────────────────────────────────────┐
│   Main App (e.g., MyBlog)              │
│   - Has integration tokens             │
│   - Calls integration services         │
└───────────┬────────────────────────────┘
            │
            │ HTTP Request
            │ Authorization: Bearer {jwt}
            ↓
┌────────────────────────────────────────┐
│   Integration Service                  │
│   (Payment Gateway / Courier / etc.)   │
│   - Linbik.Server (this)               │
│   - Validates JWT with public key      │
│   - Extracts userId, userName          │
│   - Looks up user data in own DB       │
└────────────────────────────────────────┘
```

---

## 🔑 JWT Token Structure

When your service receives a JWT:

```json
{
  "userId": "12345678-1234-1234-1234-123456789abc",
  "userName": "sarah_wilson",
  "nickName": "Sarah",
  "iat": 1698765432,
  "exp": 1698769032,
  "iss": "Linbik",
  "sub": "main-service-guid",
  "aud": "your-service-guid"
}
```

**Key Claims:**
- `userId` - Linbik user ID (use as foreign key)
- `userName` - User's unique username
- `nickName` - User's display name
- `iss` - Issuer (always "Linbik")
- `sub` - Main service that requested the token
- `aud` - Your service ID (audience)
- `exp` - Expiration timestamp (Unix)

---

## 🔄 Token Validation Flow

1. **Main app calls your API**
   ```
   POST /api/charge
   Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
   ```

2. **ASP.NET Core middleware validates JWT**
   - Checks signature with your public key
   - Verifies issuer ("Linbik")
   - Verifies audience (your service ID)
   - Checks expiration time

3. **If valid** → Request proceeds to controller
   ```csharp
   [Authorize]  // This ensures JWT is valid
   public async Task<IActionResult> Charge(...)
   ```

4. **Extract user info from claims**
   ```csharp
   var userId = User.FindFirstValue("userId");
   ```

5. **Look up user data in your database**
   ```csharp
   var userData = await _db.Users.FindAsync(Guid.Parse(userId));
   ```

---

## 📋 Configuration Options

```csharp
public class LinbikServerOptions
{
    /// <summary>
    /// RSA public key (PEM format) from Linbik.App
    /// </summary>
    public string PublicKey { get; set; }
    
    /// <summary>
    /// Expected token issuer
    /// Default: "Linbik"
    /// </summary>
    public string Issuer { get; set; } = "Linbik";
    
    /// <summary>
    /// Your service package name (token audience)
    /// </summary>
    public string Audience { get; set; }
    
    /// <summary>
    /// Validate token lifetime
    /// Default: true
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;
    
    /// <summary>
    /// Clock skew tolerance in minutes
    /// Default: 1 minute
    /// </summary>
    public int ClockSkewMinutes { get; set; } = 1;
}
```

---

## 🔗 Related Libraries

- **Linbik.Core** - HTTP client for Linbik.App (not needed for integration services)
- **Linbik.JwtAuthManager** - User authentication (not needed for integration services)
- **Linbik.YARP** - API Gateway (can work with Linbik.Server)

---

## 📄 License

See main repository LICENSE

---

**Version**: 2.0.0  
**Purpose**: JWT validation for integration services  
**For**: Shared services (payment, courier, survey, etc.)  
**Last Updated**: 1 Kasım 2025
