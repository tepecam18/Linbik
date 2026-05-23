using System.Security.Claims;
using Linbik.Core;
using Linbik.Core.Services;

// Linbik.Gateway örneği için PASETO v4.public token üretici dev aracı.
//
// Komutlar:
//   linbik-token keygen
//       → Yeni bir Ed25519 anahtar çifti üretir (private + public, Base64).
//
//   linbik-token issue --scheme <self|delegated|apps> --private-key <base64>
//                      [--sub <subject>] [--aud <audience>] [--exp <minutes>]
//                      [--claim key=value]... [--kid <key-id>]
//       → İmzalı PASETO token üretir.
//
// Örnek:
//   linbik-token keygen
//   linbik-token issue --scheme self --private-key <PRIV> --sub user-42 \
//                      --aud linbik-client --claim role=admin --claim email=u@x.io

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var helper = new PasetoHelperService();

return args[0].ToLowerInvariant() switch
{
    "keygen" => RunKeygen(helper),
    "issue"  => await RunIssueAsync(helper, args.Skip(1).ToArray()),
    "-h" or "--help" or "help" => RunHelp(),
    _ => RunUnknown(args[0])
};

static int RunKeygen(PasetoHelperService helper)
{
    var (priv, pub) = helper.GenerateKeyPair();
    Console.WriteLine("# PASETO v4.public (Ed25519) anahtar çifti");
    Console.WriteLine("# Private key — appsettings'e KOYMA, sadece token üretici tutar.");
    Console.WriteLine($"PrivateKeyBase64={priv}");
    Console.WriteLine();
    Console.WriteLine("# Public key — gateway appsettings.json'a koy.");
    Console.WriteLine($"PublicKeyBase64={pub}");
    return 0;
}

static async Task<int> RunIssueAsync(PasetoHelperService helper, string[] rest)
{
    var opts = ParseArgs(rest);

    if (!opts.TryGetValue("scheme", out var scheme))
    {
        Console.Error.WriteLine("HATA: --scheme zorunlu (self|delegated|apps).");
        return 2;
    }
    if (!opts.TryGetValue("private-key", out var privateKey))
    {
        Console.Error.WriteLine("HATA: --private-key zorunlu (Base64).");
        return 2;
    }

    var audience = opts.GetValueOrDefault("aud") ?? scheme switch
    {
        "self"      => "linbik-client",
        "delegated" => "linbik-delegated",
        "apps"      => "linbik-apps",
        _ => "linbik-client"
    };

    var subject = opts.GetValueOrDefault("sub") ?? "dev-user";
    var exp     = int.TryParse(opts.GetValueOrDefault("exp"), out var m) ? m : 60;
    var kid     = opts.GetValueOrDefault("kid");

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, subject),
        new("sub", subject)
    };

    // Tekrarlı --claim k=v parametreleri
    foreach (var raw in opts.GetAll("claim"))
    {
        var idx = raw.IndexOf('=');
        if (idx <= 0)
        {
            Console.Error.WriteLine($"HATA: --claim 'key=value' formatında olmalı (alınan: '{raw}').");
            return 2;
        }
        claims.Add(new Claim(raw[..idx], raw[(idx + 1)..]));
    }

    var token = await helper.CreateTokenAsync(
        claims.ToArray(),
        privateKey,
        audience,
        expirationMinutes: exp,
        keyId: kid);

    Console.WriteLine(token);
    return 0;
}

static int RunHelp()
{
    PrintUsage();
    return 0;
}

static int RunUnknown(string cmd)
{
    Console.Error.WriteLine($"Bilinmeyen komut: {cmd}");
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("""
        linbik-token — PASETO v4.public dev token üretici

        Komutlar:
          keygen                              Yeni Ed25519 anahtar çifti üret.
          issue --scheme <s> --private-key <k> [opt] İmzalı token üret.
          help                                Bu mesaj.

        issue seçenekleri:
          --scheme <self|delegated|apps>      Zorunlu.
          --private-key <base64>              Zorunlu (keygen çıktısı).
          --sub <subject>                     Varsayılan: dev-user
          --aud <audience>                    Varsayılan: scheme'e göre türetilir.
          --exp <minutes>                     Varsayılan: 60
          --claim key=value                   Tekrarlanabilir.
          --kid <key-id>                      Footer için opsiyonel.

        Örnek:
          linbik-token keygen
          linbik-token issue --scheme self --private-key <PRIV> \
                             --sub user-42 --claim role=admin
        """);
}

static MultiDict ParseArgs(string[] rest)
{
    var dict = new MultiDict();
    for (int i = 0; i < rest.Length; i++)
    {
        var a = rest[i];
        if (!a.StartsWith("--")) continue;

        var key = a[2..];
        if (i + 1 < rest.Length && !rest[i + 1].StartsWith("--"))
        {
            dict.Add(key, rest[++i]);
        }
        else
        {
            dict.Add(key, "true");
        }
    }
    return dict;
}

sealed class MultiDict
{
    private readonly Dictionary<string, List<string>> _data = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string key, string value)
    {
        if (!_data.TryGetValue(key, out var list))
            _data[key] = list = new List<string>();
        list.Add(value);
    }

    public bool TryGetValue(string key, out string value)
    {
        if (_data.TryGetValue(key, out var list) && list.Count > 0)
        {
            value = list[0];
            return true;
        }
        value = string.Empty;
        return false;
    }

    public string? GetValueOrDefault(string key)
        => _data.TryGetValue(key, out var list) && list.Count > 0 ? list[0] : null;

    public IEnumerable<string> GetAll(string key)
        => _data.TryGetValue(key, out var list) ? list : Enumerable.Empty<string>();
}
