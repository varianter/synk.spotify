using System.Net;
using System.Net.Http.Json;

namespace Synk.Spotify;

internal class SpotifyApi
{
    private readonly HttpClient client = new();
    private readonly Logger logger = new($"{nameof(SpotifyApi)}: ");

    public SpotifyApi()
    {
        client.Timeout = TimeSpan.FromMinutes(5);
    }

    public void SetAccessToken(string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
    }

    public async Task<UserProfile?> GetUserProfile()
    {
        logger.LogInfo("Getting user profile for current user.");
        var response = await client.GetAsync("https://api.spotify.com/v1/me");
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError($"Failed to get user profile. Response code was {response.StatusCode}.");
            return null;
        }
        var profile = await response.Content.ReadFromJsonAsync<UserProfile>();
        if (profile is null)
        {
            logger.LogError("Failed to get user profile. Response was empty or failed to deserialize body.");
            return null;
        }
        logger.LogInfo($"User profile retrieved.");
        return profile;
    }

    public async Task<RecentlyPlayedResponse?> GetRecentlyPlayed(DateTime lastSync)
    {
        var after = new DateTimeOffset(DateTime.SpecifyKind(lastSync, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        logger.LogInfo($"Getting recently played tracks for current user since last sync {lastSync} - {after}.");
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
        var result = await response.Content.ReadFromJsonAsync<RecentlyPlayedResponse>();
        if (result is null)
        {
            logger.LogError("Failed to get recently played tracks. Response was empty or failed to deserialize body.");
            return null;
        }
        logger.LogInfo($"Recently played tracks retrieved. Found {result.items.Length} tracks since last sync.");
        return result;
    }
}

// disable naming convetion warnings for records. This is just to make the json deserialization work without configuring it.
#pragma warning disable IDE1006
internal record RecentlyPlayedResponse(RecentlyPlayedItem[] items);
internal record RecentlyPlayedItem(Track track, DateTime played_at);
internal record Track(string id);
internal record UserProfile(string id);
#pragma warning restore IDE1006