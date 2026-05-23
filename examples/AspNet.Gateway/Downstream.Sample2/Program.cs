var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi("v1");

var app = builder.Build();

app.MapOpenApi("/openapi/v1.json");
app.MapControllers();
app.Run();
