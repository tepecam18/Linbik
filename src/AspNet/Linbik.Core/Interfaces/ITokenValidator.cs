using Linbik.Core.Responses;

namespace Linbik.Core.Interfaces;

public interface ITokenValidator
{
    Task<TokenValidatorResponse> ValidateToken(string token, string verifier);
}
