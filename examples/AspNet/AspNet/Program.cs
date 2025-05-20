using Linbik.Core;
using Linbik.JwtAuthManager;
using Linbik.Server;
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
app.UseJwtAuth();
app.UseProxy();

app.MapControllers();

app.Run();
