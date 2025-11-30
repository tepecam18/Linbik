using Linbik.Core.Extensions;
using Linbik.JwtAuthManager.Extensions;
using Linbik.YARP.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// MVC Services
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

builder.Services.AddDistributedMemoryCache();

// ✅ Linbik Core - Authentication client services (includes HttpClient resilience)
builder.Services.AddLinbik(builder.Configuration);

// ✅ Linbik JwtAuthManager - Login/callback/logout middleware
builder.Services.AddLinbikJwtAuth(builder.Configuration);

// ✅ Linbik Rate Limiting - Protect auth endpoints from abuse
builder.Services.AddLinbikRateLimiting(builder.Configuration);

// ✅ Linbik YARP - API Gateway with automatic token injection
builder.Services.AddLinbikYarp(builder.Configuration);

// Logging for development
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

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

// ✅ Rate limiting middleware (before other middleware)
app.UseLinbikRateLimiting();

app.UseSession(); // Session middleware - required for Linbik auth

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ✅ Linbik middleware pipeline
app.UseLinbikYarpProxy(); // API Gateway with automatic JWT injection

// Map endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Test}/{action=Index}/{id?}");

// ✅ Map Linbik OAuth endpoints (login, refresh, logout)
app.MapLinbikEndpoints();

// ✅ Map integration service proxy endpoints
// Pattern: /{packageName}/{**path} -> {serviceBaseUrl}/{path}
// Automatically injects JWT token from integration_{packageName} cookie
app.MapLinbikIntegrationProxy();

app.Run();
