using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using Synk.Spotify;

var configurationRoot = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var spotifyConfiguration = configurationRoot.GetSection("Spotify").Get<SpotifyConfiguration>()
    ?? throw new("Spotify configuration not found");

var cockroachConfiguration = configurationRoot.GetSection("CockroachDB").Get<CockroachConfiguration>()
    ?? throw new("CockroachDB configuration not found");

var logger = new Logger();

await using var dbContext = new CockroachDbContext(cockroachConfiguration);

var tokenStore = new TokenStore(dbContext);
logger.LogInfo("Retrieving tokens.");
var tokens = await tokenStore.GetTokens();
logger.LogInfo($"Retrieved tokens.");

var userStore = new UserStore(dbContext);
var recentlyPlayedStore = new RecentlyPlayedStore(dbContext);

var tokenRefresher = new TokenRefresher(spotifyConfiguration);

foreach (var token in tokens)
{
    string accessToken;
    if (token.IsExpired)
    {
        logger.LogInfo($"Refreshing token for user {token.UserId ?? "unknown"}");
        var refreshedToken = await tokenRefresher.RefreshTokenAsync(token);
        if (refreshedToken is null)
        {
            // Refresh token no longer valid either. Continue to next token.
            logger.LogWarning($"Refresh token no longer valid for user {token.UserId ?? "unknown"}. Skipping.");
            continue;
        }
        accessToken = refreshedToken.AccessToken;
        logger.LogInfo("Token refreshed. Updating token in database.");
        await tokenStore.UpdateToken(refreshedToken);
        logger.LogInfo("Token in database updated.");
    }
    else
    {
        logger.LogInfo("Access token still valid. Using existing token.");
        accessToken = token.AccessToken;
    }

    var api = new SpotifyClient(accessToken);
    try
    {
        string userId;
        if (token.UserId is null)
        {
            logger.LogInfo("User not found in database. Retrieving user profile from Spotify.");
            var user = await api.UserProfile.Current();
            logger.LogInfo("User profile retrieved. Creating user in database.");
            await userStore.CreateUser(user.Id);
            logger.LogInfo("User created in database. Updating token with user id.");
            await tokenStore.SetUserForToken(token.Id, user.Id);
            logger.LogInfo("Token updated with user id.");
            userId = user.Id;
        }
        else
        {
            logger.LogInfo("Token is already linked to user. Using user id from token.");
            userId = token.UserId;
        }

        // This should always get a value since we just created the user if it was null, and it is a foreign key.
        logger.LogInfo("Retrieving user info from database. Including last sync time.");
        var userInfo = await userStore.GetUserInfo(userId)
            ?? throw new Exception("User not found");

        var lastSync = userInfo.LastSync ?? DateTime.MinValue;
        var lastSyncUnixMilliseconds = new DateTimeOffset(DateTime.SpecifyKind(lastSync, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        logger.LogInfo($"Last sync time: {lastSync}. Unix milliseconds: {lastSyncUnixMilliseconds}.");
        // TODO: handle more than 50 recently played tracks since last sync
        // NOTE: This is not really a problem since this will run every x minutes anyway and get the next 50 each time.
        logger.LogInfo($"Retrieving recently played tracks from Spotify for user {userId} since last sync.");
        var recentlyPlayedResponse = await api.Player.GetRecentlyPlayed(new PlayerRecentlyPlayedRequest
        {
            Limit = 50,
            After = lastSyncUnixMilliseconds,
        });
        if (recentlyPlayedResponse.Items?.Count is 0 or null)
        {
            // No recently played tracks since last sync. Nothing to do.
            logger.LogInfo("No recently played tracks since last sync. Nothing to do.");
            continue;
        }

        logger.LogInfo($"Recently played tracks retrieved. Found {recentlyPlayedResponse.Items.Count} new tracks. Adding to database.");
        var recentlyPlayed = recentlyPlayedResponse.Items.Select(track => new RecentlyPlayed(userId, track.Track.Id, track.PlayedAt));
        await recentlyPlayedStore.AddRecentlyPlayed(recentlyPlayed);
        logger.LogInfo("Recently played tracks added to database. Updating last sync time.");

        await userStore.UpdateLastSync(userId, recentlyPlayed.Max(track => track.PlayedAt));
        logger.LogInfo("Last sync time updated.");
    }
    catch (APIUnauthorizedException)
    {
        logger.LogWarning("APIUnauthorizedException caught. Token no longer valid. Skipping.");
        // Token may have expired in the time between checking and using it.
        // TODO: Try to refresh token and retry once more.

        // Another possibility is that the token does not have the right scopes.
        // TODO: check scopes.
    }
    catch
    {
        logger.LogError("Unknown error. Skipping.");
    }
}
