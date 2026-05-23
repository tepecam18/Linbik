using System.IdentityModel.Tokens.Jwt;

namespace Linbik.JwtAuthManager.Services;

/// <summary>
/// Default <see cref="ILocalJwtTokenReader"/> implementation. Uses
/// <see cref="JwtSecurityTokenHandler.ReadJwtToken(string)"/> to parse claims without
/// performing signature validation (validation is performed by the JwtBearer handler).
/// </summary>
internal sealed class LocalJwtTokenReader : ILocalJwtTokenReader
{
    public Dictionary<string, string> Read(string token)
    {
        if (string.IsNullOrEmpty(token))
            return [];

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
                return [];

            var jwt = handler.ReadJwtToken(token);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var claim in jwt.Claims)
            {
                result[claim.Type] = claim.Value;
            }
            return result;
        }
        catch
        {
            return [];
        }
    }
}
