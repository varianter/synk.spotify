using Microsoft.Azure.Functions.Worker;

namespace Synk.Spotify;

public class PlayedTracksFetcher
{
    private readonly TokenStore tokenStore;
    private readonly UserStore userStore;
    private readonly MusicStore musicStore;
    private readonly PlayedTracksStore playedTracksStore;
    private readonly TokenRefresher tokenRefresher;
    private readonly SpotifyApi api;

    public PlayedTracksFetcher(
        TokenStore tokenStore,
        UserStore userStore,
        MusicStore musicStore,
        PlayedTracksStore playedTracksStore,
        TokenRefresher tokenRefresher,
        SpotifyApi api)
    {
        this.tokenStore = tokenStore;
        this.userStore = userStore;
        this.musicStore = musicStore;
        this.playedTracksStore = playedTracksStore;
        this.tokenRefresher = tokenRefresher;
        this.api = api;
    }

    [Function(nameof(PlayedTracksFetcher))]
    public async Task Run([TimerTrigger("0 0 * * * *")] ScheduleInfo info)
    {
        var logger = new Logger($"{nameof(Program)}: ");

        if (info.IsPastDue)
        {
            logger.LogWarning("Timer is past due.");
        }
        logger.LogInfo($"Last run: {info.ScheduleStatus.Last}");
        logger.LogInfo($"Last updated: {info.ScheduleStatus.LastUpdated}");
        logger.LogInfo($"Next run: {info.ScheduleStatus.Next}");

        var tokens = await tokenStore.GetTokens();

        foreach (var token in tokens)
        {
            var hasRefreshed = false;
        retry:
            api.SetAccessToken(token.AccessToken);

            try
            {
                if (token.UserId is null)
                {
                    logger.LogWarning("Token does not have a user associated with it. Skipping.");
                    continue;
                }

                // This should always get a value since we just created the user if it was null, and it is a foreign key.
                var userInfo = await userStore.GetUserInfo(token.UserId)
                    ?? throw new Exception("User not found");

                var recentlyPlayedResponse = await api.GetRecentlyPlayed(userInfo.LastSync ?? DateTime.MinValue);
                if (recentlyPlayedResponse is null)
                {
                    logger.LogWarning("Failed to retrieve recently played tracks. Skipping.");
                    goto missingimages;
                }
                if (recentlyPlayedResponse.items?.Length is 0 or null)
                {
                    logger.LogInfo("No recently played tracks since last sync. Nothing to do.");
                    goto missingimages;
                }

                await musicStore.StoreMissingTrackInfo(recentlyPlayedResponse);

                var recentlyPlayed = recentlyPlayedResponse.items.Select(track => new PlayedTrack(token.UserId, track.track.id, track.played_at));
                await playedTracksStore.AddPlayedTrack(recentlyPlayed);

                await userStore.UpdateLastSync(token.UserId, recentlyPlayed.Max(track => track.PlayedAt));

            missingimages:
                // Scan database for artists that are missing images and get them;
                var artists = await musicStore.GetArtistsWithoutImages();
                foreach (var artist in artists)
                {
                    var artistInfo = await api.GetArtistDetails(artist.id);
                    if (artistInfo is null)
                    {
                        logger.LogWarning($"Failed to retrieve artist info for {artist.id}. Skipping.");
                        continue;
                    }
                    await musicStore.UpdateArtistImage(artist.id, artistInfo.BigImageUrl);
                }
            }
            catch (SpotifyUnauthorizedException)
            {
                logger.LogWarning("Token is unauthorized. Refreshing token.");
                var refreshedToken = await tokenRefresher.RefreshTokenAsync(token);
                if (refreshedToken is null)
                {
                    logger.LogError("Failed to refresh token. Skipping.");
                    continue;
                }
                await tokenStore.UpdateToken(refreshedToken);
                if (!hasRefreshed)
                {
                    hasRefreshed = true;
                    goto retry;
                }
            }
        }
    }
}

public class ScheduleInfo
{
    public required ScheduleStatus ScheduleStatus { get; set; }

    public bool IsPastDue { get; set; }
}

public class ScheduleStatus
{
    public DateTime Last { get; set; }

    public DateTime Next { get; set; }

    public DateTime LastUpdated { get; set; }
}
