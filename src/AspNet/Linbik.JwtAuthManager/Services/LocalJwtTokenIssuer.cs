using Linbik.JwtAuthManager.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Linbik.JwtAuthManager.Services;

/// <summary>
/// Cookie tabanlı kimlik doğrulama için HS256 JWT access token üretir.
/// Servisler arası (Application) token üretiminde kullanılmaz; yalnızca <c>authToken</c> cookie'si içindir.
/// </summary>
internal static class LocalJwtTokenIssuer
{
    private const int MinSecretKeyLength = 32; // HS256 için minimum 256-bit

    /// <summary>
    /// HS256 ile imzalanmış yerel JWT access token üretir.
    /// SecretKey eksik veya kısaysa null döner ve hatayı loglar.
    /// </summary>
    internal static string? Create(
        JwtAuthOptions options,
        Core.Models.LinbikTokenResponse tokenResponse,
        DateTime accessTokenExpiry,
        ILogger logger)
    {
        if (string.IsNullOrEmpty(options.SecretKey))
        {
            logger.LogError(
                "SecretKey is not configured in JwtAuthOptions. " +
                "Please set 'Linbik:JwtAuth:SecretKey' in appsettings.json");
            return null;
        }

        if (options.SecretKey.Length < MinSecretKeyLength)
        {
            logger.LogError(
                "SecretKey is too short. Minimum length is {MinLength} characters for HS256. " +
                "Current length: {CurrentLength}",
                MinSecretKeyLength, options.SecretKey.Length);
            return null;
        }

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, tokenResponse.UserId.ToString()),
            new(JwtRegisteredClaimNames.PreferredUsername, tokenResponse.Username),
            new(JwtRegisteredClaimNames.Name, tokenResponse.DisplayName ?? tokenResponse.Username),
        ];

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.JwtIssuer,
            audience: options.JwtAudience,
            claims: claims,
            expires: accessTokenExpiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
