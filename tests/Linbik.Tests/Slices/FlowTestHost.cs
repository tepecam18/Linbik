using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Linbik.Core;
using Linbik.Core.Attributes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Linbik.Tests.Slices;

/// <summary>Records what the probe endpoint and the fake auth handlers did during a request.</summary>
internal sealed class ProbeState
{
    private int _handlerCalls;

    public int HandlerCalls => _handlerCalls;

    public ConcurrentQueue<string> AuthenticateCalls { get; } = new();

    public void RecordHandlerCall() => Interlocked.Increment(ref _handlerCalls);
}

/// <summary>
/// Fake authentication handler: succeeds when the <c>X-Test-Auth</c> header contains this
/// scheme's name, otherwise reports <c>NoResult</c>.
/// </summary>
internal sealed class FakeSchemeHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ProbeState state) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string HeaderName = "X-Test-Auth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        state.AuthenticateCalls.Enqueue(Scheme.Name);

        if (!Request.Headers[HeaderName].Contains(Scheme.Name))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity([new Claim("sub", "test-user")], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

internal sealed record FilterResponse(int StatusCode, bool? IsSuccess, string? Title, string? Message, string? User);

/// <summary>
/// In-memory TestServer host with a single probe endpoint (<c>/probe</c>) guarded by the
/// filter under test. Fake Linbik schemes are registered on demand.
/// </summary>
internal sealed class FlowTestHost : IAsyncDisposable
{
    public const string ProbePath = "/probe";

    private readonly WebApplication _app;

    private FlowTestHost(WebApplication app, HttpClient client, ProbeState state)
    {
        _app = app;
        Client = client;
        State = state;
    }

    public HttpClient Client { get; }

    public ProbeState State { get; }

    /// <param name="allowedFlows">
    /// Flows put into <see cref="LFlowAuthorizeAttribute"/> endpoint metadata; <c>null</c> = no metadata at all.
    /// </param>
    /// <param name="registeredSchemes">
    /// Fake schemes to register. <c>null</c> = do not call <c>AddAuthentication</c> at all
    /// (no <see cref="IAuthenticationSchemeProvider"/> in DI).
    /// </param>
    public static async Task<FlowTestHost> StartAsync<TFilter>(
        string[]? allowedFlows,
        string[]? registeredSchemes = null)
        where TFilter : class, IEndpointFilter
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        var state = new ProbeState();
        builder.Services.AddSingleton(state);

        if (registeredSchemes is not null)
        {
            var auth = builder.Services.AddAuthentication();
            foreach (var scheme in registeredSchemes)
                auth.AddScheme<AuthenticationSchemeOptions, FakeSchemeHandler>(scheme, null);
        }

        var app = builder.Build();

        var endpoint = app.MapGet(ProbePath, (HttpContext context, ProbeState probe) =>
        {
            probe.RecordHandlerCall();
            return Results.Ok(new { user = context.User.Identity?.AuthenticationType });
        }).AddEndpointFilter<TFilter>();

        if (allowedFlows is not null)
            endpoint.WithMetadata(new LFlowAuthorizeAttribute(allowedFlows));

        await app.StartAsync();
        return new FlowTestHost(app, app.GetTestClient(), state);
    }

    public async Task<HttpResponseMessage> SendAsync(
        string? flowHeader = null, string[]? authSchemes = null, string[]? extraFlowHeaders = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProbePath);
        if (flowHeader is not null)
            request.Headers.TryAddWithoutValidation(LinbikDefaults.HeaderFlow, flowHeader);
        foreach (var extra in extraFlowHeaders ?? [])
            request.Headers.TryAddWithoutValidation(LinbikDefaults.HeaderFlow, extra);
        if (authSchemes is not null)
            request.Headers.TryAddWithoutValidation(FakeSchemeHandler.HeaderName, authSchemes);

        return await Client.SendAsync(request);
    }

    public static async Task<FilterResponse> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var root = json.RootElement;

        bool? isSuccess = root.TryGetProperty("isSuccess", out var s) && s.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? s.GetBoolean()
            : null;

        string? title = null, message = null;
        if (root.TryGetProperty("friendlyMessage", out var fm) && fm.ValueKind == JsonValueKind.Object)
        {
            title = fm.TryGetProperty("title", out var t) ? t.GetString() : null;
            message = fm.TryGetProperty("message", out var m) ? m.GetString() : null;
        }

        string? user = root.TryGetProperty("user", out var u) ? u.GetString() : null;
        return new FilterResponse((int)response.StatusCode, isSuccess, title, message, user);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
