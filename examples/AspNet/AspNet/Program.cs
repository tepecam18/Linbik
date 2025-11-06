using Linbik.Core.Extensions;
using Linbik.JwtAuthManager.Extensions;
using Linbik.YARP.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// MVC Services
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

builder.Services.AddDistributedMemoryCache();

// ✅ Linbik Core - OAuth 2.0 client services
builder.Services.AddLinbik(builder.Configuration);

// ✅ Linbik JwtAuthManager - Login/callback/logout middleware
builder.Services.AddLinbikJwtAuth();

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

app.UseRouting();

// ✅ Linbik middleware pipeline
app.UseLinbikAuthMiddleware(); // Handles /linbik/login, /linbik/callback, /linbik/logout
app.UseLinbikYarpProxy(); // API Gateway with automatic JWT injection

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Test}/{action=Index}/{id?}");

app.Run();
