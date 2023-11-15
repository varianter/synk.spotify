using Microsoft.Azure.Functions.Worker;

namespace Synk.Spotify;

public class PlaylistUpdater
{
    private readonly TokenStore tokenStore;
    private readonly SpotifyApi api;
    private readonly MusicStore musicStore;
    private readonly TokenRefresher tokenRefresher;
    private readonly Logger logger = new($"{nameof(PlaylistUpdater)}: ");

    public PlaylistUpdater(TokenStore tokenStore, SpotifyApi api, MusicStore musicStore, TokenRefresher tokenRefresher)
    {
        this.tokenStore = tokenStore;
        this.api = api;
        this.musicStore = musicStore;
        this.tokenRefresher = tokenRefresher;
    }

    [Function(nameof(PlaylistUpdater))]
    public async Task Run([TimerTrigger("0 30 0 * * *")] ScheduleInfo info) // 12:30 AM every day
    {
        var token = await tokenStore.GetSynkTokens();
        api.SetAccessToken(token.AccessToken);

        var hasRefreshed = false;
    retry:
        try
        {
            var newPlaylists = await musicStore.GetNewPlaylists();
            foreach (var newPlaylist in newPlaylists)
            {
                var spotifyPlaylist = await api.CreatePlaylist(token?.UserId ?? throw new Exception("No user"), newPlaylist);
                await musicStore.MapNewSpotifyPlaylistToPlaylist(newPlaylist, spotifyPlaylist?.id ?? throw new Exception("No playlist id"));
                var playlistItems = await musicStore.GetPlaylistItems(newPlaylist.id);
                await api.PopulatePlaylist(spotifyPlaylist.id, playlistItems);
            }

            var outdatedPlaylists = await musicStore.GetOutdatedPlaylists();
            foreach (var outdatedPlaylist in outdatedPlaylists)
            {
                var currentPlaylist = await musicStore.GetCurrentPlaylist(outdatedPlaylist);
                if (currentPlaylist is null)
                {
                    continue;
                }

                var playlistItems = await musicStore.GetPlaylistItems(outdatedPlaylist.id);
                await api.ClearPlaylist(outdatedPlaylist.spotify_id!, playlistItems);
                playlistItems = await musicStore.GetPlaylistItems(currentPlaylist.id);
                await api.PopulatePlaylist(outdatedPlaylist.spotify_id!, playlistItems);
                await musicStore.UpdatePlaylistIdForSpotifyPlaylist(outdatedPlaylist.spotify_id!, currentPlaylist.id);
            }
        }
        catch (SpotifyUnauthorizedException)
        {
            if (hasRefreshed)
            {
                logger.LogWarning("Token is still unauthorized... Aborting.");
                return;
            }

            logger.LogWarning("Token is unauthorized. Refreshing token.");
            var refreshedToken = await tokenRefresher.RefreshTokenAsync(token);
            if (refreshedToken is null)
            {
                logger.LogError("Failed to refresh token. Skipping.");
                return;
            }
            await tokenStore.UpdateToken(refreshedToken);
            hasRefreshed = true;
            goto retry;
        }
    }
}
