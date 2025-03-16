namespace Linbik.Interfaces;

public interface ITokenValidator
{
    Task<TokenValidatorResponse> ValidateToken(string token);
}
