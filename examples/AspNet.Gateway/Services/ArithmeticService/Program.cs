using Linbik.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // Her operation'a [LFlowAuthorize] kuralından türetilen linbik-flows extension'ını ekler.
    options.AddLinbikFlowExtension();
});

var app = builder.Build();

// OpenAPI dokümanını tüm ortamlarda yayınla; gateway de okuyabilsin.
app.MapOpenApi();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
