using System.Net;
using System.Net.Http.Json;

namespace Synk.Spotify;

internal class SpotifyApi
{
    private readonly HttpClient client = new();
    private readonly Logger logger = new($"{nameof(SpotifyApi)}: ");

    public SpotifyApi()
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    }

    public void SetAccessToken(string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
    }

    public async Task<UserProfile?> GetUserProfile()
    {
        logger.LogInfo("Getting user profile for current user.");
        try
        {
            var response = await client.GetAsync("https://api.spotify.com/v1/me");
            if (response.StatusCode is HttpStatusCode.TooManyRequests)
            {
                // Wait specified seconds in retry-after header or default to 5 minutes.
                var retryInSeconds = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60 * 5;
                logger.LogWarning($"Too many requests. Retrying in {retryInSeconds} seconds.");
                await Task.Delay(TimeSpan.FromSeconds(retryInSeconds));
                return await GetUserProfile();
            }
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
        catch (TaskCanceledException)
        {
            logger.LogWarning("Request timed out. Retrying in 5 minutes.");
            // TODO: Show some kind of progress indicator.
            await Task.Delay(TimeSpan.FromMinutes(5));
            return await GetUserProfile();
        }
    }

    public async Task<RecentlyPlayedResponse?> GetRecentlyPlayed(DateTime lastSync)
    {
        var after = new DateTimeOffset(DateTime.SpecifyKind(lastSync, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        logger.LogInfo($"Getting recently played tracks for current user since last sync {lastSync} - {after}.");
        try
        {
            var response = await client.GetAsync($"https://api.spotify.com/v1/me/player/recently-played?limit=50&after={after}");
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.TooManyRequests)
                {
                    // Wait specified seconds in retry-after header or default to 5 minutes.
                    var retryInSeconds = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60 * 5;
                    logger.LogWarning($"Too many requests. Retrying in {retryInSeconds} seconds.");
                    await Task.Delay(TimeSpan.FromSeconds(retryInSeconds));
                    return await GetRecentlyPlayed(lastSync);
                }
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
        catch (TaskCanceledException)
        {
            logger.LogWarning("Request timed out. Retrying in 5 minutes.");
            // TODO: Show some kind of progress indicator.
            await Task.Delay(TimeSpan.FromMinutes(5));
            return await GetRecentlyPlayed(lastSync);
        }
    }
}

// disable naming convetion warnings for records. This is just to make the json deserialization work without configuring it.
#pragma warning disable IDE1006
internal record RecentlyPlayedResponse(RecentlyPlayedItem[] items);
internal record RecentlyPlayedItem(Track track, DateTime played_at);
internal record Track(string id, Album album, Artist[] artists, string name, int duration_ms);
internal record Album(string id, string name, Image[] images)
{
    public string BigImageUrl => images.Length > 0 ? images.First().url : "No image found.";
};

internal record Image(string url, int width, int height);
internal record Artist(string id, string name);
internal record UserProfile(string id);
#pragma warning restore IDE1006
