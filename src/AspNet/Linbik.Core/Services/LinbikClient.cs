using Linbik.Core.Configuration;
using Linbik.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Linbik.Core.Services;

/// <summary>
/// HTTP client for communicating with Linbik authorization server
/// Handles OAuth 2.0 flow: redirect → code exchange → token management
/// </summary>
public class LinbikClient(
    HttpClient httpClient,
    IOptions<LinbikOptions> options,
    IHttpContextAccessor contextAccessor) : IAuthService
{
    private readonly HttpClient _httpClient = ConfigureHttpClient(httpClient, options.Value);
    private readonly LinbikOptions _options = options.Value;
    private readonly IHttpContextAccessor _contextAccessor = contextAccessor;

    private const string SessionKeyUserProfile = "linbik_user_profile";
    private const string SessionKeyRefreshToken = "linbik_refresh_token";
    private const string SessionKeyTokens = "linbik_integration_tokens";

    private static HttpClient ConfigureHttpClient(HttpClient client, LinbikOptions options)
    {
        if (!string.IsNullOrEmpty(options.ServerUrl))
        {
            client.BaseAddress = new Uri(options.ServerUrl);
        }

        client.DefaultRequestHeaders.Add("ApiKey", options.ApiKey);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }

    /// <summary>
    /// Redirect to Linbik authorization endpoint
    /// </summary>
    public Task RedirectToLinbikAsync(HttpContext context, string? returnUrl = null, string? codeChallenge = null)
    {
        // Build authorization URL
        var authUrl = $"{_options.ServerUrl}{_options.AuthorizationEndpoint}";

        if (!string.IsNullOrEmpty(codeChallenge))
        {
            authUrl += $"/{codeChallenge}";
        }

        // Store return URL in session
        if (!string.IsNullOrEmpty(returnUrl))
        {
            context.Session.SetString("linbik_return_url", returnUrl);
        }

        // Redirect to Linbik
        context.Response.Redirect(authUrl);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Exchange authorization code for tokens
    /// </summary>
    public async Task<TokenResponse?> ExchangeCodeForTokensAsync(string code)
    {
        try
        {
            // Prepare request
            var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint);
            request.Headers.Add("Code", code);

            var payload = new
            {
                serviceId = _options.ServiceId
            };

            var requestData = JsonSerializer.Serialize(payload);

            request.Content = new StringContent(requestData, Encoding.UTF8, "application/json");

            // Send request
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Token exchange failed: {response.StatusCode} - {error}");
                return null;
            }

            // Parse response
            var json = await response.Content.ReadAsStringAsync();

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null)
                return null;

            // Store in session
            var context = _contextAccessor.HttpContext;
            if (context != null)
            {
                // Store user profile
                context.Session.SetString(SessionKeyUserProfile, JsonSerializer.Serialize(new
                {
                    tokenResponse.UserId,
                    tokenResponse.UserName,
                    tokenResponse.NickName
                }));

                // Store refresh token
                context.Session.SetString(SessionKeyRefreshToken, tokenResponse.RefreshToken);

                // Store integration tokens
                var tokenDict = tokenResponse.Integrations.ToDictionary(
                    i => i.ServicePackage,
                    i => new { i.Token, i.ExpiresAt }
                );
                context.Session.SetString(SessionKeyTokens, JsonSerializer.Serialize(tokenDict));
            }

            return tokenResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exchanging code: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get current user profile from session
    /// </summary>
    public Task<UserProfile?> GetUserProfileAsync(HttpContext context)
    {
        var profileJson = context.Session.GetString(SessionKeyUserProfile);
        if (string.IsNullOrEmpty(profileJson))
            return Task.FromResult<UserProfile?>(null);

        try
        {
            var profile = JsonSerializer.Deserialize<UserProfile>(profileJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Load integration tokens
            var tokensJson = context.Session.GetString(SessionKeyTokens);
            if (!string.IsNullOrEmpty(tokensJson))
            {
                var tokens = JsonSerializer.Deserialize<Dictionary<string, TokenData>>(tokensJson);
                if (tokens != null && profile != null)
                {
                    profile.IntegrationTokens = tokens.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Token
                    );
                }
            }

            return Task.FromResult(profile);
        }
        catch
        {
            return Task.FromResult<UserProfile?>(null);
        }
    }

    /// <summary>
    /// Get integration tokens from session
    /// </summary>
    public Task<List<IntegrationToken>> GetIntegrationTokensAsync(HttpContext context)
    {
        try
        {
            var tokensJson = context.Session.GetString(SessionKeyTokens);
            if (string.IsNullOrEmpty(tokensJson))
                return Task.FromResult(new List<IntegrationToken>());

            var tokenDict = JsonSerializer.Deserialize<Dictionary<string, TokenData>>(tokensJson);
            if (tokenDict == null)
                return Task.FromResult(new List<IntegrationToken>());

            var tokens = tokenDict.Select(kvp => new IntegrationToken
            {
                ServicePackage = kvp.Key,
                PackageName = kvp.Key, // Alias
                Token = kvp.Value.Token,
                AccessToken = kvp.Value.Token, // Alias
                ExpiresAt = kvp.Value.ExpiresAt,
                BaseUrl = string.Empty, // Session'da baseUrl tutmuyoruz
                ServiceName = kvp.Key // Fallback
            }).ToList();

            return Task.FromResult(tokens);
        }
        catch
        {
            return Task.FromResult(new List<IntegrationToken>());
        }
    }

    /// <summary>
    /// Refresh expired tokens
    /// </summary>
    public async Task<bool> RefreshTokensAsync(HttpContext context)
    {
        var refreshToken = context.Session.GetString(SessionKeyRefreshToken);
        if (string.IsNullOrEmpty(refreshToken))
            return false;

        try
        {
            // Prepare refresh request
            var request = new HttpRequestMessage(HttpMethod.Post, _options.RefreshEndpoint);
            request.Headers.Add("RefreshToken", refreshToken);

            // Send request
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                // Refresh failed - clear session
                await LogoutAsync(context);
                return false;
            }

            // Parse new tokens
            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null)
                return false;

            // Update session with new tokens
            var tokenDict = tokenResponse.Integrations.ToDictionary(
                i => i.ServicePackage,
                i => new TokenData { Token = i.Token, ExpiresAt = i.ExpiresAt }
            );
            context.Session.SetString(SessionKeyTokens, JsonSerializer.Serialize(tokenDict));
            context.Session.SetString(SessionKeyRefreshToken, tokenResponse.RefreshToken);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing tokens: {ex.Message}");
            await LogoutAsync(context);
            return false;
        }
    }

    /// <summary>
    /// Clear authentication session
    /// </summary>
    public Task LogoutAsync(HttpContext context)
    {
        context.Session.Remove(SessionKeyUserProfile);
        context.Session.Remove(SessionKeyRefreshToken);
        context.Session.Remove(SessionKeyTokens);
        return Task.CompletedTask;
    }

    #region Legacy Implementation

    [Obsolete("Use GetUserProfileAsync instead")]
    public async Task<string> GetUserIdAsync(HttpContext context)
    {
        var profile = await GetUserProfileAsync(context);
        return profile?.UserId.ToString() ?? string.Empty;
    }

    #endregion

    private class TokenData
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
