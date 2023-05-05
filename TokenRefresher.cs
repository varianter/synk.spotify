using System.Net.Http.Json;
using System.Text;

namespace Synk.Spotify;

public class TokenRefresher
{
    private readonly HttpClient client = new();

    private const string TokenEndpoint = "https://accounts.spotify.com/api/token";
    private readonly string authHeaderValue;

    public TokenRefresher()
    {
        var clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID")
            ?? throw new("SPOTIFY_CLIENT_ID environment variable not set");
        var clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET")
            ?? throw new("SPOTIFY_CLIENT_SECRET environment variable not set");

        authHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
    }

    public async Task<Token?> RefreshTokenAsync(string refreshToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };

        var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };


        request.Headers.Authorization = new("Basic", authHeaderValue);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var newTokens = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>();
        if (newTokens is null)
        {
            return null;
        }

        return new(newTokens.access_token, refreshToken, DateTime.UtcNow.AddSeconds(newTokens.expires_in));
    }
}

// Ignore naming rule violations for this internal record. It makes deserializing the json simpler.
#pragma warning disable IDE1006 // Naming Styles
internal record SpotifyTokenResponse(string access_token, string token_type, string scope, long expires_in, string refresh_token);
#pragma warning restore IDE1006 // Naming Styles
