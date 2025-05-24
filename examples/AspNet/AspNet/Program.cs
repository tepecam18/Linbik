using AspNet.Repositories;
using Linbik.Core;
using Linbik.JwtAuthManager;
using Linbik.Server;
using Linbik.Server.Interfaces;
using Linbik.YARP;
using Linbik.YARP.Interfaces;
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

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("Custom API");
    options.Title = "Custom API";
});


app.UseHttpsRedirection();
app.UseRouting();
//add this line
app.UseLinbikServer(); // Enable Linbik server endpoints
app.UseJwtAuth();
app.UseProxy();

app.MapControllers();

app.Run();
