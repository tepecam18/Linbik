# 🔐 Linbik Framework

**Federated Identity Platform with OAuth 2.0 Authorization Code Flow**

Linbik is a complete OAuth 2.0 authentication framework for .NET that enables multi-service federated identity management with per-service JWT tokens.

---

## 🎯 What is Linbik?

Linbik connects distributed services through a single verified identity. It's **NOT a data warehouse** - it's an authentication layer that lets services communicate securely using a shared identity reference.

### Core Concept

```
User → Authenticates once (cookie, 7 days)
     → Service requests authorization
     → Linbik issues authorization code (10 min, single-use)
     → Service exchanges code for tokens (API Key auth)
     → Service gets: user profile + integration service JWTs + refresh token
     → Service manages own session
```

### Problem and Solution

#### ❌ Traditional Approach

```
User → Separate account for each service
     → Separate password for each service
     → Duplicate data entry (address, payment)
     → Data inconsistency
```

#### ✅ Linbik Approach

```
User → Authenticate once on Linbik
     → All services recognize same identity
     → Data stays in each service's database
     → Services communicate via identity reference
```

---

## 📦 NuGet Packages

| Package | Description | Version |
|---------|-------------|---------|
| **Linbik.Core** | Core interfaces and models | 2.0.0 |
| **Linbik.JwtAuthManager** | RSA-256 JWT signing and validation | 2.0.0 |
| **Linbik.Server** | Authorization server repository interfaces | 2.0.0 |
| **Linbik.YARP** | Multi-service token provider for YARP | 2.0.0 |

---

## 🚀 Quick Start

### 1. Authorization Server (Linbik.App)

```bash
# Clone the repository
git clone https://github.com/tepecam18/Linbik.git
cd Linbik/src/Clients/Linbik.App

# Configure database
# Edit appsettings.json with your PostgreSQL connection string

# Run migrations
dotnet ef database update

# Start server
dotnet run
```

Server will run at `https://localhost:5001`

### 2. Client Application (AspNet.Examples)

```bash
cd examples/AspNet/AspNet

# Configure Linbik connection
# Edit appsettings.json:
{
  "Linbik": {
    "ServerUrl": "https://localhost:5001",
    "ServiceId": "your-service-guid",
    "ApiKey": "linbik_your_api_key"
  }
}

# Start application
dotnet run
```

### 3. Test OAuth Flow

```bash
# 1. Start OAuth flow
GET http://localhost:5000/pkce/start

# Response:
{
  "authorizationUrl": "https://localhost:5001/auth/{serviceId}/{codeChallenge}",
  "codeVerifier": "abc123..."  # Store this in session
}

# 2. Open authorization URL in browser
# Login and grant permissions

# 3. You'll be redirected to callback:
# http://localhost:5000/oauth/callback?code=auth_xyz...

# 4. View token info
GET http://localhost:5000/oauth/token-info

# Response:
{
  "userId": "user-guid",
  "userName": "sarah_wilson",
  "nickName": "Sarah",
  "integrations": [
    {
      "servicePackage": "payment-gateway",
      "token": "eyJhbGci...",
      "expiresAt": "2025-11-01T13:00:00Z"
    }
  ],
  "refreshToken": "refresh_abc...",
  "refreshExpiresAt": "2025-12-01T12:00:00Z"
}
```

---

## 🏗️ Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│                   Authorization Server                      │
│                    (Linbik.App)                            │
│  - User authentication (cookie-based)                       │
│  - Authorization code generation                            │
│  - Token exchange endpoint                                  │
│  - Service registration                                     │
│  - User consent management                                  │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  │ OAuth 2.0 Flow
                  │
┌─────────────────▼───────────────────────────────────────────┐
│                   Client Application                        │
│                  (Your Service)                            │
│  - Uses Linbik.Core, Linbik.JwtAuthManager                 │
│  - Redirects user to Linbik for auth                       │
│  - Exchanges code for tokens (API Key)                     │
│  - Stores tokens in session                                │
│  - Calls integration services with JWTs                    │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  │ JWT Authentication
                  │
┌─────────────────▼───────────────────────────────────────────┐
│              Integration Services                          │
│        (Payment Gateway, Courier, etc.)                    │
│  - Validates JWT with public key                           │
│  - Extracts userId from token claims                       │
│  - Looks up user data in own database                      │
│  - Performs requested operation                            │
└─────────────────────────────────────────────────────────────┘
```

### OAuth 2.0 Flow Diagram

```
┌─────────┐                                    ┌────────────┐
│  User   │                                    │   MyBlog   │
└────┬────┘                                    └─────┬──────┘
     │                                               │
     │  1. Click "Login with Linbik"                 │
     │◄──────────────────────────────────────────────┤
     │                                               │
     │  2. Redirect /auth/{serviceId}/{challenge?}   │
     ├──────────────────────────────────────────────►│
     │                                               │
┌────▼──────┐                                        │
│  Linbik   │                                        │
└────┬──────┘                                        │
     │  3. Login + Consent Screen                    │
     │  4. Generate Authorization Code               │
     │  5. Redirect with code                        │
     ├──────────────────────────────────────────────►│
     │                                               │
     │  6. POST /oauth/token (ApiKey + Code)         │
     │◄──────────────────────────────────────────────┤
     │                                               │
     │  7. Return:                                   │
     │     - User profile                            │
     │     - Integration tokens (JWT)                │
     │     - Refresh token                           │
     ├──────────────────────────────────────────────►│
     │                                               │
     │  8. Call integration service                  │
     │     Authorization: Bearer {jwt}               │
     ├──────────────────────────────────────────────►│
     │                                         ┌─────▼──────┐
     │                                         │  Payment   │
     │                                         │  Gateway   │
     │                                         └─────┬──────┘
     │                                               │
     │  9. Validate JWT (RSA public key)             │
     │  10. Process payment                          │
     │◄──────────────────────────────────────────────┤
```

---

## ✨ Key Features

### 1. OAuth 2.0 Authorization Code Flow
- ✅ **Standard compliance** with Authorization Code Flow
- ✅ **PKCE support** (RFC 7636) for public clients
- ✅ **Short-lived codes** (10 minutes, single-use)
- ✅ **Cookie-based user auth** (7-day sessions)
- ✅ **API Key authentication** for token exchange

### 2. Multi-Service Integration
- ✅ **Per-service JWT tokens** signed with individual RSA keys
- ✅ **User consent management** - Users choose which services to grant access
- ✅ **Service relationships** - Main services integrate with specialized services
- ✅ **Automatic token caching** with YARP integration

### 3. RSA-256 JWT Signing
- ✅ **Asymmetric cryptography** - Private keys sign, public keys verify
- ✅ **2048-bit key pairs** for each integration service
- ✅ **PKCS#8 private keys** and **X.509 SPKI public keys**
- ✅ **Standard JWT claims** (iss, sub, aud, exp, iat)

### 4. Refresh Token Management
- ✅ **Long-lived tokens** (30 days default)
- ✅ **Token revocation** support
- ✅ **Automatic refresh** with YARP token provider
- ✅ **Secure session storage**

### 5. Security Features
- ✅ **PKCE** (Proof Key for Code Exchange)
- ✅ **IP Whitelisting** (CIDR notation support)
- ✅ **Anti-CSRF tokens** on all POST actions
- ✅ **PBKDF2 password hashing** with 128-bit salt
- ✅ **Secure session cookies** (HttpOnly, Secure, SameSite)

---

## 📚 Documentation

### Package Documentation

- [**Linbik.Core**](src/AspNet/Linbik.Core/README.md) - Core interfaces and models
- [**Linbik.JwtAuthManager**](src/AspNet/Linbik.JwtAuthManager/README.md) - JWT signing and validation
- [**Linbik.Server**](src/AspNet/Linbik.Server/README.md) - Authorization server interfaces
- [**Linbik.YARP**](src/AspNet/Linbik.YARP/README.md) - YARP token provider

### Examples

- [**AspNet.Examples**](examples/AspNet/AspNet/README.md) - Complete OAuth client implementation
- [**Nuxt.Examples**](examples/nuxt/README.md) - Frontend integration (Coming soon)

### Guides

- [**Migration Guide**](MIGRATION_GUIDE.md) - Migrate from v1.x to v2.0
- [**Linbik.App README**](src/Clients/Linbik.App/README.md) - Authorization server documentation
- [**Copilot Instructions**](src/Clients/Linbik.App/.github/copilot-instructions.md) - AI agent guide

---

## 💡 Usage Scenarios

### Scenario 1: E-Commerce Ecosystem

**Actors**:
- **ShopX**: E-commerce site (Main service)
- **PaymentPro**: Payment processor (Integration service)
- **FastShip**: Courier company (Integration service)

**Flow**:

```csharp
// 1. User clicks "Buy" on ShopX
// ShopX redirects to Linbik
Response.Redirect($"https://linbik.com/auth/{shopXServiceId}/{codeChallenge}");

// 2. User logs in and grants permissions to PaymentPro + FastShip
// Linbik redirects back with authorization code
// https://shopx.com/callback?code=auth_xyz...

// 3. ShopX exchanges code for tokens
var tokenResponse = await ExchangeCodeForTokens(authCode);

// tokenResponse contains:
// - User profile (userId, userName, nickName)
// - Integration tokens:
//   - PaymentPro JWT (signed with PaymentPro's private key)
//   - FastShip JWT (signed with FastShip's private key)
// - Refresh token

// 4. ShopX calls PaymentPro with JWT
var paymentToken = tokenResponse.Integrations
    .First(i => i.ServicePackage == "paymentpro").Token;

await httpClient.PostAsync("https://paymentpro.com/charge", new
{
    linbik_user_id = tokenResponse.UserId,
    amount = 15000
}, headers: new { Authorization = $"Bearer {paymentToken}" });

// 5. PaymentPro validates JWT with its public key
// Looks up user's saved card by linbik_user_id
// Processes payment

// 6. ShopX calls FastShip with different JWT
var courierToken = tokenResponse.Integrations
    .First(i => i.ServicePackage == "fastship").Token;

await httpClient.PostAsync("https://fastship.com/ship", new
{
    linbik_user_id = tokenResponse.UserId,
    product = "laptop"
}, headers: new { Authorization = $"Bearer {courierToken}" });

// 7. FastShip validates JWT with its public key
// Looks up user's address by linbik_user_id
// Creates shipment
```

**Result**: User didn't enter payment card or address. Services communicated via identity reference.

### Scenario 2: Microservices with YARP

```csharp
// API Gateway with YARP + Linbik integration
// Automatically injects correct JWT for each service

// appsettings.json
{
  "ReverseProxy": {
    "Routes": {
      "payment": {
        "ClusterId": "payment-cluster",
        "Match": { "Path": "/api/payment/{**catch-all}" },
        "Transforms": [
          { "RequestHeader": "X-Service-Package", "Set": "payment-gateway" }
        ]
      },
      "courier": {
        "ClusterId": "courier-cluster",
        "Match": { "Path": "/api/courier/{**catch-all}" },
        "Transforms": [
          { "RequestHeader": "X-Service-Package", "Set": "courier-service" }
        ]
      }
    }
  }
}

// YARP Transform automatically:
// 1. Reads X-Service-Package header
// 2. Gets cached JWT for that service
// 3. Injects Authorization: Bearer {jwt}
// 4. Proxies request to target service

// Client just calls:
await httpClient.PostAsync("/api/payment/charge", content);
await httpClient.PostAsync("/api/courier/ship", content);
// No manual token management!
```

---

## 🔒 Security Considerations

### Current Strengths
✅ RSA-256 JWT signing (asymmetric crypto)  
✅ Per-service key pairs (2048-bit)  
✅ Short-lived authorization codes (10 min)  
✅ Short-lived access tokens (1 hour)  
✅ Long-lived refresh tokens (30 days)  
✅ PKCE support for public clients  
✅ IP whitelisting (CIDR notation)  
✅ Anti-CSRF tokens  
✅ PBKDF2 password hashing  
✅ HttpOnly secure session cookies

### Recommendations
🔶 **Hash API keys in database** (currently plain text in Linbik.App)  
🔶 **Implement rate limiting** on token endpoints  
🔶 **Add audit logging** for all auth operations  
🔶 **Implement scope system** for granular permissions  
🔶 **Add webhook system** for event notifications

---

## 🔄 Migration from v1.x

**v1.x** used simple symmetric JWT signing with shared secrets.  
**v2.0** implements full OAuth 2.0 Authorization Code Flow with asymmetric RSA signing.

### Breaking Changes

1. **JWT Signing Algorithm**: HS256 → RS256
2. **Authentication Pattern**: Direct token → Authorization Code Flow
3. **Token Format**: Single token → Multi-service tokens
4. **Service Model**: Shared key → Per-service RSA keys

### Migration Steps

1. **Update package references** to v2.0
2. **Implement ILinbikServerRepository** with OAuth methods
3. **Generate RSA key pairs** for integration services
4. **Update client code** to use authorization code flow
5. **Implement token caching** with refresh support

See [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md) for detailed instructions.

---

## 🛠️ Development

### Build All Projects

```bash
# From repository root
dotnet build Linbik.sln
```

### Run Tests

```bash
cd examples/AspNet/AspNet

# Use test-scenarios.http with VS Code REST Client extension
# Or use curl/Postman with provided examples
```

### Create New Service

```bash
# 1. Register service in Linbik.App
POST https://localhost:5001/Service/Create
{
  "Name": "My Service",
  "PackageName": "my-service",
  "BaseUrl": "https://myservice.com",
  "CallbackPath": "/auth/callback",
  "IsIntegrationService": true  # If it receives JWT tokens
}

# 2. Copy ServiceId and ApiKey from response

# 3. Configure in client application
{
  "Linbik": {
    "ServiceId": "service-guid",
    "ApiKey": "linbik_api_key"
  }
}

# 4. If integration service, download RSA public key
GET https://localhost:5001/Service/Edit/{serviceId}

# 5. Configure JWT validation in integration service
// See Linbik.JwtAuthManager README for examples
```

---

## 📄 License

This project is under active development and is not recommended for production use yet.

**Version**: 2.0.0  
**Contact**: info@linbik.com  
**Repository**: https://github.com/tepecam18/Linbik

---

## 🙏 Acknowledgments

This platform is built with the vision of enabling distributed services to communicate securely and seamlessly through federated identity.

**Linbik** - _One identity, infinite connections_ 🌐

---

**Last Updated**: 1 Kasım 2025
