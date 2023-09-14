using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace Synk.Spotify;

public class TokenRefresher
{
    private readonly HttpClient client = new();
    private readonly Logger logger = new($"{nameof(TokenRefresher)}: ");

    private const string TokenEndpoint = "https://accounts.spotify.com/api/token";
    private readonly string clientId;
    private readonly string authHeaderValue;

    public TokenRefresher(IOptions<SpotifyConfiguration> options)
    {
        var configuration = options.Value;
        clientId = configuration.ClientId;
        authHeaderValue = Convert.ToBase64String(Encoding.Default.GetBytes($"{clientId}:{configuration.ClientSecret}"));
    }

    public async Task<Token?> RefreshTokenAsync(Token token)
    {
        logger.LogInfo("Refreshing token.");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = token.RefreshToken,
            ["client_id"] = clientId,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };


        request.Headers.Authorization = new("Basic", authHeaderValue);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError($"Failed to refresh token. Response code was {response.StatusCode}.");
            return null;
        }

        var newTokens = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>();
        if (newTokens is null)
        {
            logger.LogError("Failed to refresh token. Response was empty or failed to deserialize body.");
            return null;
        }

        logger.LogInfo("Token refreshed.");
        return token with
        {
            AccessToken = newTokens.access_token,
            RefreshToken = newTokens.refresh_token ?? token.RefreshToken
        };
    }
}

// Ignore naming rule violations for this public record. It makes deserializing the json simpler.
#pragma warning disable IDE1006 // Naming Styles
public record SpotifyTokenResponse(string access_token, string token_type, string scope, long expires_in, string? refresh_token);
#pragma warning restore IDE1006 // Naming Styles
