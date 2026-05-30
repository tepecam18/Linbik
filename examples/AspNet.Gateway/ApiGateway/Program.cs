using ApiGateway.Auth;
using ApiGateway.Docs;
using ApiGateway.Middleware;
using ApiGateway.Transforms;
using Linbik.Core.Extensions;
using Linbik.PasetoAuthManager.Extensions;
using Linbik.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// MVC + OpenAPI (linbik-flows extension transformer dahil — gateway'in kendi
// `/openapi/v1.json` dokümanı da PasetoAuthManager endpoint'leri için
// `linbik-flows: ["*"]` üretir, böylece self doc'ta görünürler).
builder.Services.AddControllers();
builder.Services.AddOpenApi(opt => opt.AddLinbikFlowExtension());
// CORS — Linbik.App dashboard'undaki "API Test Konsolu" sayfası tarayıcıdan
// doğrudan bu gateway'e istek atar (proxy yok). İzin verilen origin'ler
// `Cors:AllowedOrigins` altından okunur.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("LinbikAppOrigins", policy =>
    {
        if (allowedOrigins.Length == 0)
            return; // hiçbir origin'e izin verme
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("WWW-Authenticate", "Linbik-Trace-Id");
    });
});
// LinbikGateway: downstream OpenAPI aggregator + 3 filtered doc + Scalar.
// YARP route/cluster konfigürasyonu da LinbikGateway:Sources içinden üretilir;
// appsettings.json'da ayrı ReverseProxy bölümüne ihtiyaç yoktur.
builder.Services.AddLinbikGatewayDocs(builder.Configuration);

// Linbik: Self (LinbikScheme, cookie tabanlı) + PasetoAuth endpoint'leri
//   +  Server: Delegated & Application bearer şemaları (Linbik platformunun
//   Ed25519 public key'i ile doğrulanan kullanıcı / S2S token'ları).
// Self ve Delegated/Application FARKLI anahtarlar kullanır:
//   Self                 → Linbik:PasetoAuth (Local SharedKey, cookie reader)
//   Delegated/Application → Linbik:Server   (Public key, Authorization: Bearer)
builder.Services
    .AddLinbik(builder.Configuration.GetSection("Linbik"))
    .AddLinbikPasetoAuth()
    .AddLinbikServer(builder.Configuration.GetSection("Linbik:Server"));

// Yalnız policy'leri kayıt eder:
//   LinbikAuthorize (Self)         — PasetoAuthManager tarafından kayıt edildi
//   LinbikDelegatedAuthorize       — AddLinbikServer'ın eklediği Delegated şema
//   LinbikApplicationAuthorize     — AddLinbikServer'ın eklediği Application şema
builder.Services.AddLinbikGatewayAuth();

// YARP reverse proxy: route + cluster otomatik üretildi (LinbikGateway:Sources)
// + claim → header transform.
builder.Services
    .AddReverseProxy()
    .LoadFromLinbikGateway(builder.Configuration)
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
//app.UseMiddleware<LinbikHeaderSanitizationMiddleware>();

app.UseRouting();

// CORS routing'ten sonra, auth'tan önce — preflight (OPTIONS) istekleri
// authentication'a takılmadan policy'ye gitsin.
app.UseCors("LinbikAppOrigins");

app.UseAuthentication();
app.UseAuthorization();

// PasetoAuthManager endpoint'leri (login / callback / refresh / logout).
app.UseLinbikPasetoAuth();

app.MapControllers();

// LinbikGateway: filtered OpenAPI JSON endpoints + Scalar UI'leri.
// /openapi/self.json + /docs/self yalnız Dev (anonim);
// /openapi/delegated.json, /openapi/apps.json ve ilgili /docs/* sayfaları
// cookie auth (LinbikAuthorize) zorunlu — anonim ziyaretçi login'e yönlendirilir.
app.MapLinbikGatewayDocs(app.Environment);
app.MapLinbikScalar(app.Environment);

// YARP rotaları: per-route AuthorizationPolicy appsettings.json'da tanımlı.
app.MapReverseProxy();

app.Run();

