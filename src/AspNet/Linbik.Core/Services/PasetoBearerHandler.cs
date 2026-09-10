using Linbik.Core.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Linbik.Core.Services;

/// <summary>
/// PASETO Bearer şeması için per-scheme ayarlar.
/// Her şema (Delegated/Application) ayrı bir options instance'ı ile yapılandırılır.
/// </summary>
public sealed class PasetoBearerOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// PASETO modu. Varsayılan <see cref="PasetoMode.Public"/> (v4.public, Ed25519).
    /// </summary>
    public PasetoMode Mode { get; set; } = PasetoMode.Public;

    /// <summary>
    /// (v4.public modu) PASETO v4.public token'larını doğrulamak için kullanılacak Ed25519 public key (Base64-encoded, 32 bytes).
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// (v4.local modu) PASETO v4.local token'larını çözmek ve doğrulamak için kullanılacak simetrik shared key (Base64-encoded, 32 bytes).
    /// </summary>
    public string SharedKey { get; set; } = string.Empty;

    /// <summary>
    /// Beklenen <c>aud</c> claim değeri (genellikle package name / service identifier).
    /// </summary>
    public string ExpectedAudience { get; set; } = string.Empty;

    /// <summary>
    /// Beklenen <c>iss</c> claim değeri (genellikle Linbik authority URL'i).
    /// </summary>
    public string ExpectedIssuer { get; set; } = string.Empty;

    /// <summary>
    /// <c>true</c> ise yalnızca <c>token_type=apps</c> içeren application token'ları kabul edilir
    /// (kullanıcı token'ları reddedilir). <c>false</c> ise yalnızca user/delegated token'lar kabul edilir
    /// (apps token'lar reddedilir). Çapraz enjeksiyon koruması.
    /// </summary>
    public bool RequireApplicationToken { get; set; }

    /// <summary>
    /// Token'ı isteğin farklı bir kaynağından (ör. cookie) okumak için opsiyonel callback.
    /// Null ise varsayılan davranış uygulanır: <c>Authorization: Bearer &lt;token&gt;</c> başlığı kullanılır.
    /// Callback boş/string null döndürürse handler <see cref="AuthenticateResult.NoResult"/> döner.
    /// </summary>
    public Func<HttpRequest, string?>? TokenRetriever { get; set; }
}

/// <summary>
/// PASETO v4.public token'larını doğrulayan ASP.NET Core kimlik doğrulama işleyicisi.
/// <c>AddJwtBearer</c>'ın PASETO eşdeğeridir; Ed25519 imzalarını <see cref="IPasetoHelper"/> aracılığıyla
/// doğrular. Per-scheme yapılandırma <see cref="PasetoBearerOptions"/> üzerinden yapılır, böylece bu
/// işleyici hem <c>Linbik.Server</c> hem de <c>Linbik.Api</c> gibi bağımsız host'lar tarafından kullanılabilir.
/// </summary>
internal sealed class PasetoBearerHandler : AuthenticationHandler<PasetoBearerOptions>
{
    private readonly IPasetoHelper _pasetoHelper;

    public PasetoBearerHandler(
        IOptionsMonitor<PasetoBearerOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        IPasetoHelper pasetoHelper)
        : base(options, loggerFactory, encoder)
    {
        _pasetoHelper = pasetoHelper;
    }

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? token;

        // Custom token retriever (ör. cookie) varsa onu kullan
        if (Options.TokenRetriever is not null)
        {
            token = Options.TokenRetriever(Request);
        }
        else
        {
            // Varsayılan: Authorization: Bearer <token>
            var authHeader = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) ||
                !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.NoResult();
            }
            token = authHeader["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        // Validate PASETO token signature/encryption, lifetime, issuer and audience
        bool isValid;
        try
        {
            isValid = Options.Mode == PasetoMode.Local
                ? await _pasetoHelper.ValidateLocalTokenAsync(
                    token,
                    Options.SharedKey,
                    Options.ExpectedAudience,
                    Options.ExpectedIssuer)
                : await _pasetoHelper.ValidateTokenAsync(
                    token,
                    Options.PublicKey,
                    Options.ExpectedAudience,
                    Options.ExpectedIssuer);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            Logger.LogWarning(ex, "PASETO token validation threw an exception for scheme {Scheme}", Scheme.Name);
            await ReportSecurityEventAsync(
                isApplication: Options.RequireApplicationToken,
                eventType: LinbikSecurityEventType.AuthenticationFailed,
                message: msg,
                statusCode: 401,
                metadata: new { exception_type = ex.GetType().Name, scheme = Scheme.Name });
            return AuthenticateResult.Fail(ex);
        }

        if (!isValid)
        {
            const string msg = "PASETO token validation failed.";
            await ReportSecurityEventAsync(
                isApplication: Options.RequireApplicationToken,
                eventType: LinbikSecurityEventType.AuthenticationFailed,
                message: msg,
                statusCode: 401,
                metadata: new { scheme = Scheme.Name });
            return AuthenticateResult.Fail(msg);
        }

        // Safely read claims without re-validating signature/MAC (validation already passed above)
        var rawClaims = Options.Mode == PasetoMode.Local
            ? _pasetoHelper.GetLocalTokenClaims(token, Options.SharedKey)
            : _pasetoHelper.GetTokenClaims(token);
        var tokenType = rawClaims.GetValueOrDefault("token_type");

        // Cross-scheme injection guard — based on per-scheme RequireApplicationToken flag
        if (Options.RequireApplicationToken && tokenType != "apps")
        {
            const string msg = "Only application tokens (token_type=apps) are accepted by this scheme.";
            await ReportSecurityEventAsync(
                isApplication: false,
                eventType: LinbikSecurityEventType.ApplicationJwtInvalid,
                message: msg,
                statusCode: 401,
                metadata: new { scheme = Scheme.Name });
            return AuthenticateResult.Fail(msg);
        }

        if (!Options.RequireApplicationToken && tokenType == "apps")
        {
            const string msg = "Application tokens (token_type=apps) are not accepted by this scheme.";
            await ReportSecurityEventAsync(
                isApplication: true,
                eventType: LinbikSecurityEventType.ApplicationJwtInvalid,
                message: msg,
                statusCode: 401,
                metadata: new { scheme = Scheme.Name });
            return AuthenticateResult.Fail(msg);
        }

        // Build ClaimsPrincipal from raw claims
        var claimsList = rawClaims.Select(kvp => new Claim(kvp.Key, kvp.Value)).ToList();
        var identity = new ClaimsIdentity(claimsList, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    /// <inheritdoc/>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;

        // Only report if there was no prior AuthenticationFailed event (avoid double-reporting)
        if (properties.GetString(".Error") is null)
        {
            await ReportSecurityEventAsync(
                isApplication: Options.RequireApplicationToken,
                eventType: Options.RequireApplicationToken
                    ? LinbikSecurityEventType.ApplicationJwtInvalid
                    : LinbikSecurityEventType.AuthenticationFailed,
                message: "Authentication challenge issued (401). Missing or unreadable token.",
                statusCode: 401,
                metadata: new { scheme = Scheme.Name });
        }
    }

    /// <inheritdoc/>
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;

        var actor = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? Context.User?.FindFirst("sub")?.Value;

        Guid? sourceServiceId = null;
        if (Options.RequireApplicationToken)
        {
            var sourceClaim = Context.User?.FindFirst("source_service_id")?.Value;
            if (Guid.TryParse(sourceClaim, out var parsed)) sourceServiceId = parsed;
        }

        await ReportSecurityEventAsync(
            isApplication: Options.RequireApplicationToken,
            eventType: LinbikSecurityEventType.AuthorizationFailed,
            message: "Authenticated principal lacks required role/policy (403).",
            statusCode: 403,
            metadata: new
            {
                scheme = Scheme.Name,
                actor,
                source_service_id = sourceServiceId,
                roles = Context.User?.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray(),
            });
    }

    // ─── Security event reporting ─────────────────────────────────────────────

    private async Task ReportSecurityEventAsync(
        bool? isApplication,
        string eventType,
        string message,
        int statusCode,
        object? metadata)
    {
        try
        {
            var sink = Context.RequestServices.GetService<ILinbikSecurityEventSink>();
            if (sink is null or NoOpSecurityEventSink) return;

            await sink.ReportAsync(new LinbikSecurityEvent
            {
                EventType = eventType,
                Message = message,
                HttpStatusCode = statusCode,
                RequestPath = Request.Path.Value,
                RequestMethod = Request.Method,
                RemoteIp = Context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                Metadata = metadata,
            }, Context.RequestAborted);
        }
        catch
        {
            // Swallow: event reporting must never break the auth flow.
        }
    }
}
