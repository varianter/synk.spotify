using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Synk.Spotify;

public class SpotifyApi
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

    public async Task<Playlist?> CreatePlaylist(string userId, SynkPlaylist playlist)
    {
        logger.LogInfo($"Creating playlist {playlist.name} for user {userId}.");
        try
        {
            var response = await client.PostAsJsonAsync($"https://api.spotify.com/v1/users/{userId}/playlists", new { name = playlist.group_path + " - " + playlist.name });

            var suggestedAction = await CheckResponse(response, $"Failed to create playlist {playlist.name} for user {userId}");
            if (suggestedAction is SuggestedAction.Retry)
            {
                return await CreatePlaylist(userId, playlist);
            }

            logger.LogInfo($"Playlist {playlist.name} created for user {userId}.");

            var result = await response.Content.ReadFromJsonAsync<Playlist>();
            return result;
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Request timed out. Retrying in 5 minutes.");
            await Task.Delay(TimeSpan.FromMinutes(5));
            return await CreatePlaylist(userId, playlist);
        }
    }

    public async Task<ArtistDetails?> GetArtistDetails(string artistId)
    {
        logger.LogInfo($"Getting artist details for {artistId}.");
        try
        {
            var response = await client.GetAsync($"https://api.spotify.com/v1/artists/{artistId}");

            var suggestedAction = await CheckResponse(response, $"Failed to get artist details for {artistId}");
            if (suggestedAction is SuggestedAction.Retry)
            {
                return await GetArtistDetails(artistId);
            }

            var artistDetails = await response.Content.ReadFromJsonAsync<ArtistDetails>();
            if (artistDetails is null)
            {
                logger.LogError($"Failed to get artist details for {artistId}. Response was empty or failed to deserialize body.");
                return null;
            }
            logger.LogInfo($"Artist details retrieved for {artistId}.");
            return artistDetails;
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Request timed out. Retrying in 5 minutes.");
            await Task.Delay(TimeSpan.FromMinutes(5));
            return await GetArtistDetails(artistId);
        }
    }

    public async Task<UserProfile?> GetUserProfile()
    {
        logger.LogInfo("Getting user profile for current user.");
        try
        {
            var response = await client.GetAsync("https://api.spotify.com/v1/me");
            var suggestedAction = await CheckResponse(response, "Failed to get user profile");
            if (suggestedAction is SuggestedAction.Retry)
            {
                return await GetUserProfile();
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

            var suggestedAction = await CheckResponse(response, "Failed to get recently played tracks.");
            if (suggestedAction is SuggestedAction.Retry)
            {
                return await GetRecentlyPlayed(lastSync);
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

    private enum SuggestedAction
    {
        Retry,
        None
    }

    private async Task<SuggestedAction> CheckResponse(HttpResponseMessage response, string failureMessage)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            throw new SpotifyUnauthorizedException();
        }

        if (response.StatusCode is HttpStatusCode.Forbidden)
        {
            logger.LogError($"Forbidden. Body: {await response.Content.ReadAsStringAsync()}.");
            throw new Exception("Forbidden.");
        }

        if (response.StatusCode is HttpStatusCode.TooManyRequests)
        {
            // Wait specified seconds in retry-after header or default to 5 minutes.
            var retryInSeconds = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60 * 5;
            logger.LogWarning($"Too many requests. Retrying in {retryInSeconds} seconds.");
            await Task.Delay(TimeSpan.FromSeconds(retryInSeconds));
            return SuggestedAction.Retry;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning($"{failureMessage}. Response code was {response.StatusCode}.");
        }

        return SuggestedAction.None;
    }

    internal async Task PopulatePlaylist(string id, IEnumerable<PlaylistItem> playlistItems)
    {
        var response = await client.PostAsync($"https://api.spotify.com/v1/playlists/{id}/tracks",
            new StringContent(JsonSerializer.Serialize(new { uris = playlistItems.Select(x => $"spotify:track:{x.track_id}") }), Encoding.UTF8, "application/json"));

        var suggestedAction = await CheckResponse(response, $"Failed to populate playlist {id}");
        if (suggestedAction is SuggestedAction.Retry)
        {
            await PopulatePlaylist(id, playlistItems);
        }

        logger.LogInfo($"Populated playlist {id}: {response.StatusCode}");
    }

    internal async Task ClearPlaylist(string id, IEnumerable<PlaylistItem> playlistItems)
    {
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"https://api.spotify.com/v1/playlists/{id}/tracks")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { tracks = playlistItems.Select(x => new { uri = $"spotify:track:{x.track_id}" }) }), Encoding.UTF8, "application/json")
        });

        var suggestedAction = await CheckResponse(response, $"Failed to clear playlist {id}");
        if (suggestedAction is SuggestedAction.Retry)
        {
            await ClearPlaylist(id, playlistItems);
        }

        logger.LogInfo($"Cleared playlist {id}: {response.StatusCode}");
    }
}

public class SpotifyUnauthorizedException : Exception { }

// disable naming convetion warnings for records. This is just to make the json deserialization work without configuring it.
#pragma warning disable IDE1006
public record RecentlyPlayedResponse(RecentlyPlayedItem[] items);
public record RecentlyPlayedItem(Track track, DateTime played_at);
public record Track(string id, Album album, Artist[] artists, string name, int duration_ms, string? preview_url);
public record Album(string id, string name, Image[] images, string release_date)
{
    public string BigImageUrl => images.Length > 0 ? images.First().url : "No image found.";
};

public record Image(string url, int width, int height);
public record Artist(string id, string name);
public record UserProfile(string id);
public record ArtistDetails(string id, Image[] images)
{
    public string BigImageUrl => images.Length > 0 ? images.First().url : "No image found.";
};
public record Playlist(string id);

#pragma warning restore IDE1006
