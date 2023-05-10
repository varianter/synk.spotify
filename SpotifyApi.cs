using System.Net;
using System.Net.Http.Json;

namespace Synk.Spotify;

internal class SpotifyApi
{
    private readonly HttpClient client = new();
    private readonly Logger logger = new();

    public void SetAccessToken(string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
    }

    public async Task<UserProfile?> GetUserProfile()
    {
        var response = await client.GetAsync("https://api.spotify.com/v1/me");
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                logger.LogWarning("Access token not valid.");
            }
            else
            {
                logger.LogWarning($"Failed to get user profile. Response code was {response.StatusCode}.");
            }
            return null;
        }
        return await response.Content.ReadFromJsonAsync<UserProfile>();
    }

    public async Task<RecentlyPlayedResponse?> GetRecentlyPlayed(DateTime lastSync)
    {
        var after = new DateTimeOffset(DateTime.SpecifyKind(lastSync, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var response = await client.GetAsync($"https://api.spotify.com/v1/me/player/recently-played?limit=50&after={after}");
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                logger.LogWarning("Access token not valid.");
            }
            else
            {
                logger.LogWarning($"Failed to get recently played tracks. Response code was {response.StatusCode}.");
            }
            return null;
        }
        return await response.Content.ReadFromJsonAsync<RecentlyPlayedResponse>();
    }
}

internal record RecentlyPlayedResponse(RecentlyPlayedItem[] items);
internal record RecentlyPlayedItem(Track track, DateTime played_at);
internal record Track(string id);
internal record UserProfile(string id);
