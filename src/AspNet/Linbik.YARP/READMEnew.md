# Linbik.YARP

A YARP (Yet Another Reverse Proxy) integration library for .NET applications, providing secure JWT token management and multi-cluster routing capabilities.

## 🚀 Features

- **YARP Integration** - Seamless integration with YARP reverse proxy
- **JWT Token Management** - Automatic token generation and renewal
- **Multi-Cluster Support** - Route traffic to multiple backend clusters
- **Dynamic Routing** - Configurable routing based on JWT claims
- **Secure Token Storage** - Encrypted token management
- **Load Balancing** - Built-in load balancing capabilities
- **Health Monitoring** - Cluster health status monitoring

## 📦 Installation

```bash
dotnet add package Linbik.YARP
```

## 🔧 Configuration

### Basic Setup

```csharp
// Program.cs
using Linbik.Core.Extensions;
using Linbik.YARP.Extensions;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add Linbik Core first
var linbikBuilder = builder.Services.AddLinbik(options =>
{
    options.Version = LinbikVersion.Dev2025;
    options.AppIds = new[] { "app1", "app2" };
    options.AllowAllApp = false;
});

// Add YARP services
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add Linbik YARP
linbikBuilder.AddLinbikYARP(yarpOptions =>
{
    yarpOptions.RouteId = "linbik-route";
    yarpOptions.ClusterId = "linbik-cluster";
    yarpOptions.PrivateKey = "your-secret-key-here";
    yarpOptions.PrefixPath = "/api";
    yarpOptions.Clusters = new Dictionary<string, ClusterOptions>
    {
        ["cluster1"] = new ClusterOptions
        {
            Name = "Backend Cluster 1",
            Address = "https://backend1.company.com"
        },
        ["cluster2"] = new ClusterOptions
        {
            Name = "Backend Cluster 2",
            Address = "https://backend2.company.com"
        }
    };
});

var app = builder.Build();

// Use YARP
app.MapReverseProxy();
```

### Configuration File

```json
// appsettings.json
{
  "ReverseProxy": {
    "Routes": {
      "linbik-route": {
        "ClusterId": "linbik-cluster",
        "Match": {
          "Path": "/api/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "linbik-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "https://backend1.company.com"
          },
          "destination2": {
            "Address": "https://backend2.company.com"
          }
        }
      }
    }
  },
  "Linbik": {
    "YARP": {
      "RouteId": "linbik-route",
      "ClusterId": "linbik-cluster",
      "PrivateKey": "your-secret-key-here",
      "PrefixPath": "/api",
      "Clusters": {
        "cluster1": {
          "Name": "Backend Cluster 1",
          "Address": "https://backend1.company.com"
        },
        "cluster2": {
          "Name": "Backend Cluster 2",
          "Address": "https://backend2.company.com"
        }
      }
    }
  }
}
```

## 🏗️ Architecture

### Core Components

- **MultiJwtTokenProvider** - JWT token generation and management
- **YARPExtensions** - Configuration and service registration
- **YARPOptions** - Configuration options
- **ClusterOptions** - Cluster configuration

### Interfaces

- **ITokenProvider** - Token generation contract

## 🔐 Authentication Flow

### Token Generation Process

```csharp
public class MultiJwtTokenProvider : ITokenProvider
{
    public async Task<string> GetTokenAsync(string clusterName)
    {
        // Generate JWT token for specific cluster
        var token = GenerateJwtToken(clusterName);
        
        // Store token in cache
        await CacheTokenAsync(clusterName, token);
        
        return token;
    }
}
```

### JWT Token Claims

The generated JWT token includes the following claims:

```json
{
  "cluster": "cluster1",
  "tenant_id": "default",
  "user_type": "App",
  "exp": 1640995200
}
```

## 📋 Usage Examples

### Controller Integration

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProxyController : ControllerBase
{
    private readonly ITokenProvider _tokenProvider;
    
    public ProxyController(ITokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }
    
    [HttpGet("cluster/{clusterName}/token")]
    public async Task<IActionResult> GetClusterToken(string clusterName)
    {
        try
        {
            var token = await _tokenProvider.GetTokenAsync(clusterName);
            return Ok(new { Token = token, Cluster = clusterName });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}
```

### Custom Token Provider Implementation

```csharp
public class CustomTokenProvider : ITokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly YARPOptions _options;
    
    public async Task<string> GetTokenAsync(string clusterName)
    {
        var client = _httpClientFactory.CreateClient();
        
        // Get token from external service
        var response = await client.PostAsJsonAsync($"{_options.Clusters[clusterName].Address}/auth/token", new
        {
            ClusterName = clusterName,
            Timestamp = DateTime.UtcNow
        });
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
            return result.Token;
        }
        
        throw new InvalidOperationException($"Failed to get token for cluster: {clusterName}");
    }
}
```

## 🔒 Security Features

### JWT Token Security

```csharp
var tokenHandler = new JwtSecurityTokenHandler();
var key = Encoding.UTF8.GetBytes(options.PrivateKey);

var tokenDescriptor = new SecurityTokenDescriptor
{
    Subject = new ClaimsIdentity(claims),
    Expires = DateTime.UtcNow.AddMinutes(30),
    SigningCredentials = new SigningCredentials(
        new SymmetricSecurityKey(key),
        SecurityAlgorithms.HmacSha512Signature
    )
};
```

### Secure Cluster Communication

- HMAC-SHA512 signature algorithm
- Configurable private key
- Token expiration validation
- Cluster-specific claims

## 🌐 Multi-Cluster Support

### Cluster Configuration

```csharp
var yarpOptions = new YARPOptions
{
    RouteId = "linbik-route",
    ClusterId = "linbik-cluster",
    PrivateKey = "your-secret-key",
    PrefixPath = "/api",
    Clusters = new Dictionary<string, ClusterOptions>
    {
        ["production"] = new ClusterOptions
        {
            Name = "Production Cluster",
            Address = "https://prod.company.com"
        },
        ["staging"] = new ClusterOptions
        {
            Name = "Staging Cluster",
            Address = "https://staging.company.com"
        },
        ["development"] = new ClusterOptions
        {
            Name = "Development Cluster",
            Address = "https://dev.company.com"
        }
    }
};
```

### Dynamic Routing

```csharp
// Route based on cluster name in JWT token
var clusterName = GetClusterFromToken(jwtToken);
var clusterAddress = yarpOptions.Clusters[clusterName].Address;

// Forward request to appropriate cluster
await ForwardRequestAsync(request, clusterAddress);
```

## 📚 API Reference

### YARPOptions

```csharp
public class YARPOptions
{
    public string RouteId { get; set; }
    public string ClusterId { get; set; }
    public string PrivateKey { get; set; }
    public Dictionary<string, ClusterOptions> Clusters { get; set; }
    public string PrefixPath { get; set; }
}
```

### ClusterOptions

```csharp
public class ClusterOptions
{
    public string Name { get; set; }
    public string Address { get; set; }
}
```

### ITokenProvider

```csharp
public interface ITokenProvider
{
    Task<string> GetTokenAsync(string clusterName);
}
```

## 🧪 Testing

### Unit Testing

```csharp
[Test]
public async Task GetTokenAsync_ShouldReturnValidToken()
{
    // Arrange
    var tokenProvider = new MultiJwtTokenProvider(Mock.Of<IOptions<YARPOptions>>());
    var clusterName = "test-cluster";
    
    // Act
    var token = await tokenProvider.GetTokenAsync(clusterName);
    
    // Assert
    Assert.IsNotNull(token);
    Assert.IsTrue(token.Length > 0);
}
```

### Integration Testing

```csharp
[Test]
public async Task YARPProxy_ShouldRouteToCorrectCluster()
{
    // Arrange
    var client = _factory.CreateClient();
    var clusterName = "cluster1";
    
    // Act
    var response = await client.GetAsync($"/api/cluster/{clusterName}/data");
    
    // Assert
    Assert.IsTrue(response.IsSuccessStatusCode);
    
    // Verify request was routed to correct cluster
    var forwardedTo = response.Headers.GetValues("X-Forwarded-To").FirstOrDefault();
    Assert.AreEqual("https://backend1.company.com", forwardedTo);
}
```

## 🚀 Performance

- **Efficient Token Generation** - Fast JWT creation
- **Async Operations** - Non-blocking token operations
- **Token Caching** - Reduce token generation overhead
- **Load Balancing** - Distribute traffic across clusters

## 🔧 Customization

### Custom Routing Logic

```csharp
public class CustomRoutingMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Custom routing logic based on JWT claims
        var clusterName = GetClusterFromJwt(context);
        var clusterAddress = GetClusterAddress(clusterName);
        
        // Set custom headers
        context.Request.Headers.Add("X-Target-Cluster", clusterName);
        context.Request.Headers.Add("X-Target-Address", clusterAddress);
        
        await next(context);
    }
}
```

### Custom Token Validation

```csharp
public class CustomTokenValidator
{
    public bool ValidateToken(string token, string clusterName)
    {
        // Custom validation logic
        var claims = ParseJwtToken(token);
        
        // Validate cluster claim
        var tokenCluster = claims.FirstOrDefault(c => c.Type == "cluster")?.Value;
        if (tokenCluster != clusterName)
        {
            return false;
        }
        
        // Validate expiration
        var expClaim = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
        if (long.TryParse(expClaim, out var exp))
        {
            var expiration = DateTimeOffset.FromUnixTimeSeconds(exp);
            if (expiration < DateTimeOffset.UtcNow)
            {
                return false;
            }
        }
        
        return true;
    }
}
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📞 Support

For support and questions:
- Create an issue in the repository
- Contact the development team
- Check the documentation

---

**Linbik.YARP** - Secure YARP integration with JWT token management for enterprise applications.
