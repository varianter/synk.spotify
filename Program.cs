using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Synk.Spotify;

var configurationRoot = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var spotifyConfiguration = configurationRoot.GetSection("Spotify").Get<SpotifyConfiguration>()
    ?? throw new("Spotify configuration not found");

var cockroachConfiguration = configurationRoot.GetSection("CockroachDB").Get<CockroachConfiguration>()
    ?? throw new("CockroachDB configuration not found");

var logger = new Logger($"{nameof(Program)}: ");

await using var dbContext = new CockroachDbContext(cockroachConfiguration);

var tokenStore = new TokenStore(dbContext);
var tokens = await tokenStore.GetTokens();

var userStore = new UserStore(dbContext);
var musicStore = new MusicStore(dbContext);
var recentlyPlayedStore = new RecentlyPlayedStore(dbContext);

var tokenRefresher = new TokenRefresher(spotifyConfiguration);

var api = new SpotifyApi();
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

        var recentlyPlayed = recentlyPlayedResponse.items.Select(track => new RecentlyPlayed(token.UserId, track.track.id, track.played_at));
        await recentlyPlayedStore.AddRecentlyPlayed(recentlyPlayed);

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
