using Linbik.Core.Models;

namespace Linbik.Core.Identity;

/// <summary>Kullanıcı kimliği taşıyan aktörler için ortak yüzey (Self, Delegated).</summary>
public interface IUserBearingActor
{
    string UserId { get; }
    string? DisplayName { get; }
    string? Username => null;
}

/// <summary>Uygulama kimliği taşıyan aktörler için ortak yüzey (Delegated, Application).</summary>
public interface IAppBearingActor
{
    string AppId { get; }
}

/// <summary>
/// Bir isteğin çağıranını temsil eden kapalı (closed) kimlik modeli. <c>Linbik-*</c> gateway
/// header'larından (bkz. <see cref="LGatewayAuthContext"/>) bir kez türetilir. Handler/Validator'lar
/// ham <c>Flow</c> string karşılaştırmaları yerine bu tip üzerinde pattern-match yapar; yeni bir
/// flow eklendiğinde exhaustive switch'ler derleyici tarafından işaretlenir.
/// </summary>
public abstract record LActor
{
    private LActor() { }

    /// <summary>Hiçbir kimlik doğrulanmadı. Yalnızca <c>[LFlowPublic]</c> slice'lara, token'sız
    /// bir çağıran isteği yaptığında ulaşılır.</summary>
    public sealed record Anonymous : LActor
    {
        public static readonly Anonymous Instance = new();
    }

    /// <summary>Self akışı: cookie tabanlı kullanıcı oturumu.</summary>
    public sealed record AsUser(string UserId, string? DisplayName) : LActor, IUserBearingActor
    {
        public string? Username { get; init; }
        /// <summary>Self oturumunun isteğe bağlı uygulama bağlamı; uygulama kimliği veya grant sağlamaz.</summary>
        public string? ApplicationContextId { get; init; }
    }

    /// <summary>Delegated akışı: bir uygulama, son kullanıcı adına çağırıyor.</summary>
    public sealed record AsDelegated(string UserId, string AppId, string? DisplayName) : LActor, IUserBearingActor, IAppBearingActor
    {
        public string? Username { get; init; }
    }

    /// <summary>Application akışı: kullanıcı yok, yalnızca uygulama kimliği var.</summary>
    public sealed record AsApplication(string AppId) : LActor, IAppBearingActor;

    /// <summary>
    /// Gateway'in enjekte ettiği <c>Linbik-*</c> header'larından çözümlenmiş <see cref="LGatewayAuthContext"/>'i
    /// bir <see cref="LActor"/>'a çevirir. Flow header'ı yoksa/tanınmıyorsa <see cref="Anonymous"/> döner.
    /// </summary>
    public static LActor FromGatewayContext(LGatewayAuthContext ctx)
    {
        string? DisplayNameClaim() => FirstClaim(ctx, "name", "username", "preferred_username", "preferred-username", "displayname", "display-name");

        return ctx.Flow?.Trim() switch
        {
            var flow when string.Equals(flow, LinbikDefaults.Flows.Self, StringComparison.OrdinalIgnoreCase) => new AsUser(
                UserId: FirstClaim(ctx, "sub") ?? "unknown-user",
                DisplayName: DisplayNameClaim())
            { Username = FirstClaim(ctx, "username", "preferred_username", "preferred-username"), ApplicationContextId = FirstClaim(ctx, "azp") },
            var flow when string.Equals(flow, LinbikDefaults.Flows.Delegated, StringComparison.OrdinalIgnoreCase) => new AsDelegated(
                UserId: FirstClaim(ctx, "sub") ?? "unknown-user",
                AppId: FirstClaim(ctx, "azp") ?? "unknown-app",
                DisplayName: DisplayNameClaim())
            { Username = FirstClaim(ctx, "username", "preferred_username", "preferred-username") },
            var flow when string.Equals(flow, LinbikDefaults.Flows.Application, StringComparison.OrdinalIgnoreCase) => new AsApplication(
                // Application akışında token'daki 'sub' claim'i uygulamanın kendisidir (kullanıcı yok).
                AppId: FirstClaim(ctx, "azp") ?? FirstClaim(ctx, "sub") ?? "unknown-app"),
            _ => Anonymous.Instance,
        };
    }

    private static string? FirstClaim(LGatewayAuthContext ctx, params string[] names)
    {
        foreach (var name in names)
        {
            if (!ctx.Claims.TryGetValue(name, out var values))
                values = ctx.Claims.FirstOrDefault(claim =>
                    string.Equals(claim.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
            if (values is { Length: > 0 } && !string.IsNullOrWhiteSpace(values[0]))
                return values[0];
        }
        return null;
    }
}

/// <summary>Yardımcı <see cref="LActor"/> uzantıları.</summary>
public static class LActorExtensions
{
    private static string? ValidId(string? id, string missing) => string.IsNullOrWhiteSpace(id) || id == missing ? null : id;
    /// <summary>Yazan/işlem yapan tarafın tekil kimliği + bunun bir uygulama olup olmadığı.
    /// <see cref="LActor.Anonymous"/> için çağrılamaz (önce ayrıca ele alınmalı).</summary>
    public static (string Id, bool IsApp) Identity(this LActor actor) => actor switch
    {
        LActor.AsUser u => (u.UserId, false),
        LActor.AsDelegated d => (d.UserId, false),
        LActor.AsApplication a => (a.AppId, true),
        LActor.Anonymous => throw new InvalidOperationException(
            "Anonymous actor'ın kimliği yok; çağırmadan önce 'actor is LActor.Anonymous' kontrolü yapın."),
        _ => throw new NotSupportedException(actor.GetType().Name),
    };

    /// <summary>Aktörle ilişkili uygulama kimliği (Delegated/Application); diğerlerinde <c>null</c>.</summary>
    public static string? AppIdOrNull(this LActor actor) => actor switch
    {
        LActor.AsDelegated d => ValidId(d.AppId, "unknown-app"),
        LActor.AsApplication a => ValidId(a.AppId, "unknown-app"),
        _ => null,
    };
    /// <summary>Kullanıcı yoksa null; Application'ın sub değeri kullanıcı kimliği olarak döndürülmez.</summary>
    public static string? UserIdOrNull(this LActor actor) => ValidId((actor as IUserBearingActor)?.UserId, "unknown-user");
    public static string? DisplayNameOrNull(this LActor actor) => (actor as IUserBearingActor)?.DisplayName;
    public static string? UsernameOrNull(this LActor actor) => (actor as IUserBearingActor)?.Username;

    /// <summary>Audit bağlamı; Self için isteğe bağlıdır. Uygulama yetkisi için AppIdOrNull kullanılmalıdır.</summary>
    public static string? ApplicationContextIdOrNull(this LActor actor)
        => actor is LActor.AsUser user ? user.ApplicationContextId : actor.AppIdOrNull();
}
