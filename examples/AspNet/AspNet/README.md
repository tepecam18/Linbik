# Linbik OAuth 2.1 Test Application# Linbik.WebApi - Example Implementation



Bu proje, Linbik OAuth 2.1 Authorization Code Flow'unu test etmek için geliştirilmiş örnek bir ASP.NET Core uygulamasıdır.A complete example web API project demonstrating how to implement and use the Linbik Authentication Framework in a real-world application.



## 🎯 Özellikler## 🚀 Overview



- ✅ OAuth 2.1 Authorization Code FlowThis project serves as a comprehensive example of how to integrate and use all Linbik components in a production-ready web API. It demonstrates best practices, proper configuration, and real-world usage patterns.

- ✅ Multi-service integration (Birden fazla entegre servis token'ı)

- ✅ PKCE (Proof Key for Code Exchange) desteği## 📦 Project Structure

- ✅ Refresh token ile token yenileme

- ✅ Integration service test endpoint'leri```

- ✅ Token bilgilerini görüntülemeLinbik.WebApi/

├── Controllers/           # API Controllers

## 📋 Ön Gereksinimler│   ├── AuthController.cs  # Authentication endpoints

│   ├── UserController.cs  # User management

1. **.NET 9.0 SDK** yüklü olmalı│   └── AppController.cs   # App authentication

2. **Linbik.App** uygulaması çalışır durumda olmalı (https://localhost:5001)├── Models/                # Data models

3. Linbik.App'te bir **Service** kaydı oluşturulmuş olmalı│   ├── LoginRequest.cs    # Login request model

│   ├── UserProfile.cs     # User profile model

## 🚀 Kurulum│   └── ApiResponse.cs     # Standard API response

├── Services/              # Business logic services

### 1. Service Kaydı Oluştur (Linbik.App'te)│   ├── UserService.cs     # User operations

│   └── AuthService.cs     # Authentication logic

Linbik.App uygulamasında `/Service/Create` sayfasına git ve yeni bir servis oluştur:├── Middleware/            # Custom middleware

│   └── LoggingMiddleware.cs # Request logging

```├── Program.cs             # Application entry point

Name: Test Application├── appsettings.json       # Configuration

PackageName: test-app└── READMEnew.md           # This documentation

BaseUrl: https://localhost:7020```

CallbackPath: /oauth/callback

IsIntegrationService: false## 🔧 Prerequisites

```

- .NET 9.0 SDK

Kaydet butonuna tıkladıktan sonra **Service ID** ve **API Key**'i not al.- Visual Studio 2022 or VS Code

- Basic understanding of ASP.NET Core

### 2. Entegre Servisler Ekle (Opsiyonel)- Linbik packages (will be installed automatically)



Eğer multi-service integration test etmek istersen:## 🚀 Quick Start



1. Payment Gateway ve Courier Service gibi entegre servisler oluştur (`IsIntegrationService: true`)### 1. Clone and Setup

2. Test Application'ın "Integrations" sekmesinde bu servisleri ekle

3. `IsEnabled: true` yap```bash

git clone https://github.com/your-org/linbik.git

### 3. appsettings.json Yapılandırmasıcd linbik/src/AspNet/Linbik.WebApi

dotnet restore

`appsettings.json` dosyasındaki `OAuth` bölümünü güncelle:dotnet build

```

```json

{### 2. Configuration

  "OAuth": {

    "LinbikBaseUrl": "https://localhost:5001",Update `appsettings.json` with your settings:

    "ServiceId": "YOUR_SERVICE_ID_HERE",

    "ApiKey": "YOUR_API_KEY_HERE",```json

    "CallbackUrl": "https://localhost:7020/oauth/callback"{

  }  "Logging": {

}    "LogLevel": {

```      "Default": "Information",

      "Microsoft.AspNetCore": "Warning"

### 4. Uygulamayı Çalıştır    }

  },

```bash  "AllowedHosts": "*",

cd examples/AspNet/AspNet  "Linbik": {

dotnet run    "Version": "dev2025",

```    "AppIds": ["webapi", "mobile", "desktop"],

    "AllowAllApp": false,

Uygulama https://localhost:7020 adresinde başlayacak.    "PublicKey": "your-public-key-here",

    "JwtAuth": {

## 🧪 Test Senaryoları      "PrivateKey": "your-jwt-secret-key",

      "PkceEnabled": true,

### Senaryo 1: Basit Authentication Flow      "AccessTokenExpiration": 15,

      "RefreshTokenExpiration": 15

1. **Authorization URL Oluştur**:    },

   ```    "Server": {

   https://localhost:5001/auth/{SERVICE_ID}      "PrivateKey": "your-server-secret-key",

   ```      "AccessTokenExpiration": 60

   Tarayıcıda bu URL'yi aç.    }

  }

2. **Login Yap**: Linbik.App'te giriş yap (veya zaten giriş yapmışsan devam et)}

```

3. **Consent Ver**: Hangi entegre servislere izin vereceğini seç

### 3. Run the Application

4. **Callback**: Otomatik olarak `https://localhost:7020/oauth/callback?code=xxx` adresine yönlendirileceksin

```bash

5. **Token Response**: JSON response'da kullanıcı bilgileri ve entegre servis token'ları göreceksindotnet run

```

### Senaryo 2: PKCE ile Authentication

The API will be available at:

1. **Code Verifier Oluştur** (JavaScript):- **Swagger UI**: https://localhost:5001/swagger

   ```javascript- **API Base**: https://localhost:5001/api

   const codeVerifier = generateRandomString(128);

   sessionStorage.setItem('code_verifier', codeVerifier);## 🏗️ Architecture

   ```

### Dependency Injection Setup

2. **Code Challenge Hesapla**:

   ```javascript```csharp

   const codeChallenge = await sha256Base64Url(codeVerifier);// Program.cs

   ```var builder = WebApplication.CreateBuilder(args);



3. **Authorization URL**:// Add Linbik Core

   ```var linbikBuilder = builder.Services.AddLinbik(options =>

   https://localhost:5001/auth/{SERVICE_ID}/{CODE_CHALLENGE}{

   ```    options.Version = LinbikVersion.Dev2025;

    options.AppIds = new[] { "webapi", "mobile", "desktop" };

4. **Callback'te Validation Yap**:    options.AllowAllApp = false;

   ```javascript});

   const response = await fetch('/oauth/callback?code=xxx');

   const data = await response.json();// Add JWT Authentication

   linbikBuilder.AddJwtAuth(jwtOptions =>

   const storedVerifier = sessionStorage.getItem('code_verifier');{

   const computedChallenge = await sha256Base64Url(storedVerifier);    jwtOptions.PrivateKey = builder.Configuration["Linbik:JwtAuth:PrivateKey"];

       jwtOptions.PkceEnabled = true;

   if (computedChallenge !== data.codeChallenge) {    jwtOptions.AccessTokenExpiration = 15;

     throw new Error('PKCE validation failed!');    jwtOptions.RefreshTokenExpiration = 15;

   }});

   ```

// Add Server Authentication

### Senaryo 3: Token Bilgilerini GörüntülemelinbikBuilder.AddLinbikServer(serverOptions =>

{

```bash    serverOptions.PrivateKey = builder.Configuration["Linbik:Server:PrivateKey"];

GET https://localhost:7020/oauth/token-info    serverOptions.AccessTokenExpiration = 60;

```});



Response:// Add custom services

```jsonbuilder.Services.AddScoped<IUserService, UserService>();

{builder.Services.AddScoped<IAuthService, AuthService>();

  "user": {```

    "id": "guid",

    "username": "sarah_wilson",### Authentication Flow

    "nickname": "Sarah"

  },```mermaid

  "integrations": [sequenceDiagram

    {    participant Client

      "serviceName": "Payment Gateway",    participant WebApi

      "servicePackage": "payment-gateway",    participant Linbik

      "expiresAt": "2025-10-31T15:30:00Z",    participant Database

      "expiresIn": 55.2,

      "isExpired": false    Client->>WebApi: POST /api/auth/login

    }    WebApi->>Linbik: Validate credentials

  ],    Linbik->>Database: Check user

  "refreshToken": {    Database->>Linbik: User data

    "expiresAt": "2025-11-30T14:30:00Z",    Linbik->>WebApi: JWT token

    "expiresIn": 29.8,    WebApi->>Client: Authentication response

    "isExpired": false```

  }

}## 📋 API Endpoints

```

### Authentication Endpoints

### Senaryo 4: Token Refresh

#### 1. User Login

```bash```http

POST https://localhost:7020/oauth/test-refreshPOST /api/auth/login

```Content-Type: application/json



Response:{

```json  "username": "john_doe",

{  "password": "secure_password"

  "success": true,}

  "message": "Tokens refreshed successfully",```

  "integrations": [

    {**Response:**

      "serviceName": "Payment Gateway",```json

      "expiresAt": "2025-10-31T16:30:00Z"{

    }  "isSuccess": true,

  ]  "data": {

}    "token": "eyJhbGciOiJIUzUxMiIsInR5cCI6IkpXVCJ9...",

```    "user": {

      "id": "123e4567-e89b-12d3-a456-426614174000",

### Senaryo 5: Integration Service Test      "username": "john_doe",

      "firstName": "John",

```bash      "lastName": "Doe",

POST https://localhost:7020/oauth/test-integration/payment-gateway      "email": "john@example.com"

Content-Type: application/json    }

  }

{}

  "amount": 1000,```

  "currency": "TRY"

}#### 2. App Authentication

``````http

POST /api/auth/app-login

Response:Content-Type: application/json

```json

{{

  "success": true,  "appId": "123e4567-e89b-12d3-a456-426614174000",

  "statusCode": 200,  "key": "app-secret-key"

  "integration": {}

    "serviceName": "Payment Gateway",```

    "baseUrl": "https://payment-gateway.com"

  },#### 3. Refresh Token

  "response": "{ ... integration service response ... }"```http

}POST /api/auth/refresh

```Authorization: Bearer {refresh_token}

```

## 📚 API Endpoints

### User Management Endpoints

| Endpoint | Method | Açıklama |

|----------|--------|----------|#### 1. Get User Profile

| `/oauth/callback` | GET | OAuth callback endpoint (authorization code alır) |```http

| `/oauth/token-info` | GET | Mevcut token bilgilerini görüntüle |GET /api/users/profile

| `/oauth/test-refresh` | POST | Refresh token ile yeni token'lar al |Authorization: Bearer {access_token}

| `/oauth/test-integration/{servicePackage}` | POST | Belirli bir entegre servisi test et |```

| `/oauth/clear-cache` | POST | Cache'i temizle (test için) |

#### 2. Update User Profile

## 🔧 Troubleshooting```http

PUT /api/users/profile

### Problem: "Authorization code is missing"Authorization: Bearer {access_token}

Content-Type: application/json

**Çözüm**: Authorization URL'yi doğru kullandığından emin ol:

```{

https://localhost:5001/auth/{DOGRU_SERVICE_ID}  "firstName": "John",

```  "lastName": "Smith",

  "email": "john.smith@example.com"

### Problem: "Token exchange failed"}

```

**Çözüm**: 

1. `appsettings.json`'daki API Key'in doğru olduğunu kontrol et#### 3. Get All Users (Admin Only)

2. Linbik.App'in çalıştığından emin ol (https://localhost:5001)```http

3. Service kaydının aktif olduğunu kontrol etGET /api/users

Authorization: Bearer {access_token}

### Problem: "Integration service not found"```



**Çözüm**: ## 🔐 Security Implementation

1. Linbik.App'te Service detay sayfasına git

2. "Integrations" sekmesinde servislerin `IsEnabled: true` olduğunu kontrol et### JWT Token Validation

3. Consent screen'de servislere izin verdiğinden emin ol

```csharp

### Problem: "Token expired"[ApiController]

[Route("api/[controller]")]

**Çözüm**: `/oauth/test-refresh` endpoint'ini kullanarak token'ları yenile.[Authorize] // Requires valid JWT token

public class UserController : ControllerBase

## 🎓 Öğrenme Kaynakları{

    private readonly ICurrentActor _currentActor;

- [OAuth 2.1 RFC 6749](https://tools.ietf.org/html/rfc6749)    private readonly IUserService _userService;

- [PKCE RFC 7636](https://tools.ietf.org/html/rfc7636)    

- [Linbik.App README](../../../src/Clients/Linbik.App/README.md)    public UserController(ICurrentActor currentActor, IUserService userService)

- [Linbik.App Copilot Instructions](../../../src/Clients/Linbik.App/.github/copilot-instructions.md)    {

        _currentActor = currentActor;

## 📝 Notlar        _userService = userService;

    }

- **Token Cache**: Token'lar in-memory cache'de saklanır. Production'da Redis veya veritabanı kullanın.    

- **Security**: HTTPS zorunludur. Development'ta self-signed certificate kullanabilirsiniz.    [HttpGet("profile")]

- **PKCE**: Production'da PKCE kullanımı önerilir (özellikle public client'lar için).    public async Task<IActionResult> GetProfile()

- **Refresh Token**: 30 gün geçerlidir. Expired olduktan sonra kullanıcının tekrar giriş yapması gerekir.    {

        if (!_currentActor.IsAuthenticated)

## 🤝 Katkıda Bulunma            return Unauthorized();

            

Pull request'ler memnuniyetle karşılanır. Büyük değişiklikler için lütfen önce bir issue açın.        var userProfile = await _userService.GetUserProfileAsync(_currentActor.UserGuid.Value);

        return Ok(userProfile);

## 📄 Lisans    }

}

Bu proje özel bir lisans altında yayınlanmaktadır.```


### Role-Based Access Control

```csharp
[HttpGet("admin/users")]
[Authorize(Roles = "Admin")] // Role-based authorization
public async Task<IActionResult> GetAllUsers()
{
    var users = await _userService.GetAllUsersAsync();
    return Ok(users);
}
```

### Tenant Isolation

```csharp
[HttpGet("tenant-data")]
public async Task<IActionResult> GetTenantData()
{
    var tenantId = _currentActor.TenantId;
    
    // Tenant-specific data access
    var data = await _dataService.GetDataForTenantAsync(tenantId);
    
    return Ok(new
    {
        TenantId = tenantId,
        Data = data,
        UserType = _currentActor.UserType.ToString()
    });
}
```

## 🧪 Testing

### Unit Testing

```csharp
[TestFixture]
public class UserControllerTests
{
    private UserController _controller;
    private Mock<ICurrentActor> _mockCurrentActor;
    private Mock<IUserService> _mockUserService;
    
    [SetUp]
    public void Setup()
    {
        _mockCurrentActor = new Mock<ICurrentActor>();
        _mockUserService = new Mock<IUserService>();
        _controller = new UserController(_mockCurrentActor.Object, _mockUserService.Object);
    }
    
    [Test]
    public async Task GetProfile_AuthenticatedUser_ReturnsProfile()
    {
        // Arrange
        var userGuid = Guid.NewGuid();
        _mockCurrentActor.Setup(x => x.IsAuthenticated).Returns(true);
        _mockCurrentActor.Setup(x => x.UserGuid).Returns(userGuid);
        
        var expectedProfile = new UserProfile { Id = userGuid, Username = "testuser" };
        _mockUserService.Setup(x => x.GetUserProfileAsync(userGuid))
            .ReturnsAsync(expectedProfile);
        
        // Act
        var result = await _controller.GetProfile();
        
        // Assert
        var okResult = result as OkObjectResult;
        Assert.IsNotNull(okResult);
        Assert.AreEqual(expectedProfile, okResult.Value);
    }
}
```

### Integration Testing

```csharp
[TestFixture]
public class AuthenticationIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    
    [SetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }
    
    [Test]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "testuser",
            Password = "testpass"
        };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        // Assert
        Assert.IsTrue(response.IsSuccessStatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Data.Token);
    }
}
```

## 🔧 Customization Examples

### Custom Middleware

```csharp
public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;
    
    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            await _next(context);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Request {Method} {Path} completed in {Duration}ms with status {StatusCode}",
                context.Request.Method,
                context.Request.Path,
                duration.TotalMilliseconds,
                context.Response.StatusCode);
        }
    }
}
```

### Custom Authentication Handler

```csharp
public class CustomAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ITokenValidator _tokenValidator;
    
    public CustomAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        ITokenValidator tokenValidator)
        : base(options, logger, encoder, clock)
    {
        _tokenValidator = tokenValidator;
    }
    
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        
        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.Fail("Token not provided");
        
        var validationResult = await _tokenValidator.ValidateToken(token, "", false);
        
        if (!validationResult.Success)
            return AuthenticateResult.Fail("Invalid token");
        
        var claims = validationResult.Claims;
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        
        return AuthenticateResult.Success(ticket);
    }
}
```

## 📊 Monitoring and Logging

### Structured Logging

```csharp
public class AuthService
{
    private readonly ILogger<AuthService> _logger;
    
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        _logger.LogInformation("Login attempt for user {Username}", request.Username);
        
        try
        {
            // Authentication logic
            var result = await AuthenticateUserAsync(request);
            
            _logger.LogInformation("User {Username} logged in successfully", request.Username);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for user {Username}", request.Username);
            throw;
        }
    }
}
```

### Performance Monitoring

```csharp
[HttpGet("performance-test")]
public async Task<IActionResult> PerformanceTest()
{
    var stopwatch = Stopwatch.StartNew();
    
    // Simulate some work
    await Task.Delay(100);
    
    stopwatch.Stop();
    
    _logger.LogInformation("Performance test completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    
    return Ok(new { ElapsedMs = stopwatch.ElapsedMilliseconds });
}
```

## 🚀 Deployment

### Docker Support

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Linbik.WebApi.csproj", "./"]
RUN dotnet restore "Linbik.WebApi.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Linbik.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Linbik.WebApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Linbik.WebApi.dll"]
```

### Environment Configuration

```bash
# Production environment variables
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://+:80;https://+:443
export Linbik__JwtAuth__PrivateKey=your-production-key
export Linbik__Server__PrivateKey=your-production-server-key
```

## 📚 Best Practices Demonstrated

### 1. **Security**
- JWT token validation
- PKCE implementation
- Role-based authorization
- Tenant isolation
- Secure configuration

### 2. **Performance**
- Async/await patterns
- Efficient dependency injection
- Structured logging
- Performance monitoring

### 3. **Maintainability**
- Clean architecture
- Separation of concerns
- Comprehensive testing
- Clear documentation

### 4. **Scalability**
- Multi-tenant support
- Configurable authentication
- Extensible middleware
- Load balancing ready

## 🔍 Troubleshooting

### Common Issues

#### 1. **JWT Token Validation Fails**
```bash
# Check configuration
dotnet user-secrets list

# Verify private key format
# Ensure PKCE is properly configured
```

#### 2. **Authentication Not Working**
```bash
# Check service registration
dotnet run --environment Development

# Verify middleware order
# Check authorization attributes
```

#### 3. **Performance Issues**
```bash
# Monitor memory usage
dotnet-counters monitor

# Check logging levels
# Verify async patterns
```

## 🛡️ Enterprise Security Features (New!)

Bu proje artık enterprise-grade güvenlik özelliklerini içermektedir:

### Rate Limiting

3 farklı rate limiting policy mevcuttur:

| Policy | Tip | Limit | Kullanım |
|--------|-----|-------|----------|
| `LinbikAuth` | Fixed Window | 10 req/dakika | Login, logout işlemleri |
| `LinbikStrict` | Token Bucket | 5 token, 2 refill/sn | Token exchange, refresh |
| `LinbikGeneral` | Fixed Window | 50 req/dakika | Genel API istekleri |

**Kullanım:**
```csharp
[EnableRateLimiting("LinbikAuth")]
public IActionResult Login() { ... }
```

**Yapılandırma (appsettings.json):**
```json
{
  "RateLimit": {
    "Enabled": true,
    "PermitLimit": 10,
    "WindowSeconds": 60,
    "UseSlidingWindow": false
  }
}
```

### HttpClient Resilience

Otomatik retry, circuit breaker ve timeout özellikleri:

- **Retry**: 3 deneme, exponential backoff
- **Circuit Breaker**: 5 hata = 30 saniye devre kesici
- **Timeout**: 30 saniye request timeout

**Yapılandırma (appsettings.json):**
```json
{
  "Resilience": {
    "Enabled": true,
    "MaxRetryAttempts": 3,
    "RetryDelayMs": 1000,
    "CircuitBreakerEnabled": true,
    "TimeoutSeconds": 30
  }
}
```

### Audit Logging

Structured logging ile tüm authentication event'leri:

**Event Tipleri:**
- `LoginAttempt`, `LoginSuccess`, `LoginFailed`
- `TokenExchangeAttempt`, `TokenExchangeSuccess`, `TokenExchangeFailed`
- `TokenRefreshAttempt`, `TokenRefreshSuccess`, `TokenRefreshFailed`
- `RateLimitExceeded`, `InvalidApiKey`, `InvalidAuthorizationCode`
- `SessionCreated`, `SessionExpired`

**Yapılandırma (appsettings.json):**
```json
{
  "Audit": {
    "Enabled": true,
    "LogSuccessfulOperations": true,
    "IncludeIpAddress": true
  }
}
```

### Performance Metrics

OpenTelemetry uyumlu metrikler:

**Mevcut Metrikler:**
| Metrik | Tip | Açıklama |
|--------|-----|----------|
| `linbik_login_attempts_total` | Counter | Toplam login denemeleri |
| `linbik_login_successes_total` | Counter | Başarılı loginler |
| `linbik_token_exchanges_total` | Counter | Token exchange sayısı |
| `linbik_rate_limit_hits_total` | Counter | Rate limit aşımları |
| `linbik_token_exchange_duration_seconds` | Histogram | Token exchange süresi |

**OpenTelemetry Entegrasyonu:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("Linbik"));
```

### Test Endpoint'leri

Yeni güvenlik özelliklerini test etmek için endpoint'ler:

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/Test/TestRateLimit` | GET | Rate limiting testi (LinbikAuth policy) |
| `/Test/TestStrictRateLimit` | GET | Strict rate limiting testi (LinbikStrict policy) |
| `/Test/Metrics` | GET | Mevcut metriklerin listesi |
| `/Test/TestAuditLog` | POST | Audit logging testi |
| `/Test/SecurityInfo` | GET | Güvenlik yapılandırması özeti |

**Örnek Kullanım:**
```bash
# Rate limit test (10 kez çağırınca 429 döner)
curl https://localhost:7020/Test/TestRateLimit

# Audit log test
curl -X POST https://localhost:7020/Test/TestAuditLog \
  -H "Content-Type: application/json" \
  -d '{"eventType": "LoginAttempt", "message": "Test audit"}'

# Security info
curl https://localhost:7020/Test/SecurityInfo
```

## 📞 Support

### Getting Help

- **Documentation**: Check Linbik framework documentation
- **Issues**: Create GitHub issues for bugs
- **Examples**: Use this project as reference
- **Community**: Join Linbik discussions

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

---

**Linbik.WebApi** - A complete example of Linbik Authentication Framework implementation.

*Use this project as a reference for implementing Linbik in your own applications.*
