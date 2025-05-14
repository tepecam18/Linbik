namespace Linbik.Core.Interfaces;

public interface ITokenValidator
{
    Task<TokenValidatorResponse> ValidateToken(string token);
}
