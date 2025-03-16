using Linbik;
using Linbik.JwtAuthManager;
using Linbik.YARP;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddLinbik(builder.Configuration)
    .AddJwtAuth(builder.Configuration, true)
    .AddProxy(builder.Configuration);

//builder.Services.AddLinbik(conf =>
//{
//    conf.appIds = new string[] { "1", "2" };
//});

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
