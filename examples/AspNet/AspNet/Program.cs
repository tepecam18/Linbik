using Linbik.Core.Extensions;
using Linbik.JwtAuthManager.Extensions;
using Linbik.Server.Extensions;
using Linbik.YARP.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// MVC Services
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();


// ✅ Linbik - Fluent builder pattern for all Linbik services
builder.Services.AddLinbik(builder.Configuration.GetSection("Linbik"))
    .AddLinbikJwtAuth(builder.Configuration.GetSection("Linbik:JwtAuth"))
    .AddLinbikServer(builder.Configuration.GetSection("Linbik:Server"))
    .AddLinbikYarp(builder.Configuration.GetSection("Linbik:YARP"));

// ✅ Linbik Integration Handler - Handles integration lifecycle events from Linbik platform
// Override with custom handler: builder.Services.AddLinbikIntegrationHandler<MyCustomHandler>();
builder.Services.AddLinbikIntegrationHandler();

// ✅ Linbik Rate Limiting - Protect auth endpoints from abuse
builder.Services.AddLinbikRateLimiting(builder.Configuration.GetSection("Linbik:RateLimiting"));

// Logging for development
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// ✅ Validate all registered Linbik modules at startup (Core + JwtAuth + Server + YARP)
app.EnsureLinbik();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("AspNet.Examples - Linbik Integration");
    options.Title = "AspNet.Examples API";
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.UseRouting();

// ✅ Rate limiting middleware - must be after UseRouting for attribute-based rate limiting to work
app.UseLinbikRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Test}/{action=Index}/{id?}");

// ✅ Map Linbik OAuth endpoints (login, refresh, logout)
app.UseLinbikJwtAuth();

// ✅ Map Linbik Integration webhook endpoints
// Receives notifications when services create/remove/toggle integrations
// Protected by LinbikS2S authentication
app.MapLinbikIntegrationEndpoints();

// ✅ Map integration service proxy endpoints
// Pattern: /{packageName}/{**path} -> {serviceBaseUrl}/{path}
// Automatically injects JWT token from integration_{packageName} cookie
app.UseLinbikYarp();

app.Run();
