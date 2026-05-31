using Linbik.Core.Extensions;
using Linbik.Slices.Generated;

var builder = WebApplication.CreateBuilder(args);

// Slice altyapısı: ILinbikSender + tüm [LinbikSlice] handler/validator kayıtları
// (source generator tarafından üretilen LinbikSlicesRegistry).
builder.Services.AddLinbikSlices();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // Her operation'a [LFlow]/[LFlowPublic]'ten türeyen linbik-flows extension'ını ekler.
    options.AddLinbikFlowExtension();
});

var app = builder.Build();

// OpenAPI dokümanını tüm ortamlarda yayınla; gateway de okuyabilsin.
app.MapOpenApi();

app.UseHttpsRedirection();

// Tüm slice endpoint'lerini map'le (flow metadata + Linbik-Flow doğrulama filter'ı dahil).
app.MapLinbikSlices();

app.Run();
