using ApiGateway.Auth;
using ApiGateway.Middleware;
using ApiGateway.Transforms;
using Linbik.Core.Extensions;
using Linbik.PasetoAuthManager.Extensions;

var builder = WebApplication.CreateBuilder(args);

// MVC + OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Linbik: Self (LinbikScheme, cookie tabanlı) + PasetoAuth endpoint'leri.
builder.Services
    .AddLinbik(builder.Configuration.GetSection("Linbik"))
    .AddLinbikPasetoAuth();

// Delegated + Application bearer şemaları ve 3 policy:
//   LinbikAuthorize (Self)        — PasetoAuthManager tarafından kayıt edildi
//   LinbikDelegatedAuthorize       — Authorization: Bearer (kullanıcı adına başka uygulama)
//   LinbikApplicationAuthorize     — Authorization: Bearer (S2S / client_credentials)
builder.Services.AddLinbikGatewayAuth();

// YARP reverse proxy + claim → header transform.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx => ctx.RequestTransforms.Add(new LinbikClaimsHeaderTransform()));

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

app.EnsureLinbik();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// ⚠️ ÖNCE: gelen istekteki Linbik-* header'larını koşulsuz sil.
// Bu adım routing/auth'tan önce gerçekleşir; dış istemciler claim spoof edemez.
app.UseMiddleware<LinbikHeaderSanitizationMiddleware>();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// PasetoAuthManager endpoint'leri (login / callback / refresh / logout).
app.UseLinbikPasetoAuth();

app.MapControllers();

// YARP rotaları: per-route AuthorizationPolicy appsettings.json'da tanımlı.
app.MapReverseProxy();

app.Run();

