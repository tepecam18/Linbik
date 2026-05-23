using Linbik.Core.Services.Interfaces;
using Linbik.PasetoAuthManager.Configuration;
using Microsoft.Extensions.Options;

namespace Linbik.PasetoAuthManager.Services;

/// <summary>
/// Default <see cref="ILocalPasetoTokenReader"/> implementation. Dispatches between
/// v4.public and v4.local claim extraction based on the configured
/// <see cref="PasetoAuthOptions.Mode"/>.
/// </summary>
internal sealed class LocalPasetoTokenReader : ILocalPasetoTokenReader
{
    private readonly IPasetoHelper _pasetoHelper;
    private readonly IOptions<PasetoAuthOptions> _options;

    public LocalPasetoTokenReader(IPasetoHelper pasetoHelper, IOptions<PasetoAuthOptions> options)
    {
        _pasetoHelper = pasetoHelper;
        _options = options;
    }

    public Dictionary<string, string> Read(string token)
    {
        if (string.IsNullOrEmpty(token))
            return [];

        var opts = _options.Value;
        try
        {
            if (opts.Mode == PasetoMode.Local)
            {
                if (string.IsNullOrEmpty(opts.SharedKeyBase64))
                    return [];
                return _pasetoHelper.GetLocalTokenClaims(token, opts.SharedKeyBase64);
            }

            return _pasetoHelper.GetTokenClaims(token);
        }
        catch
        {
            return [];
        }
    }
}
