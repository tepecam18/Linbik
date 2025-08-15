using AspNet.Repositories;
using Linbik.Core;
using Linbik.JwtAuthManager;
using Linbik.Server;
using Linbik.Server.Interfaces;
using Linbik.YARP;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services
    .AddLinbik() // Add core Linbik services
    .AddJwtAuth(true) // Enable JWT authentication for Linbik users
    .AddLinbikServer() // Enable server services for Linbik applications
    .AddProxy();// Add proxy services for Linbik applications

//builder.Services.AddLinbik(conf =>
//{
//    conf.appIds = new string[] { "1", "2" };
//});

builder.Services.AddSingleton<ILinbikServerRepository, LinbikServerRepository>();

builder.Services
    .AddAuthentication()
    .AddLinbikScheme(builder.Configuration)
    .AddLinbikAppScheme(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    // Add policies for Yarp Linbik applications
    options.AddPolicy("LinbikAppProxyPolicy", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddAuthenticationSchemes("LinbikAppScheme");
    });
});

//for test
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("Custom API");
    options.Title = "Custom API";
});

// for test
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
//add this line
app.UseLinbikServer(); // Enable Linbik server endpoints
app.UseJwtAuth();
app.UseProxy();

app.MapControllers();

app.Run();
