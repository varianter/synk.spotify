﻿using SpotifyAPI.Web;
using Synk.Spotify;

var tokenStore = new TokenStore();
var tokens = await tokenStore.GetTokens();

var userStore = new UserStore();
var recentlyPlayedStore = new RecentlyPlayedStore();
var tokenRefresher = new TokenRefresher();

foreach (var token in tokens)
{
    string accessToken;
    if (token.IsExpired)
    {
        var refreshedToken = await tokenRefresher.RefreshTokenAsync(token);
        if (refreshedToken is null)
        {
            // Refresh token no longer valid either. Continue to next token.
            continue;
        }
        accessToken = refreshedToken.AccessToken;
        await tokenStore.UpdateToken(refreshedToken);
    }
    else
    {
        accessToken = token.AccessToken;
    }

    var api = new SpotifyClient(accessToken);
    try
    {
        string userId;
        if (token.UserId is null)
        {
            var user = await api.UserProfile.Current();
            await userStore.CreateUser(user.Id);
            await tokenStore.SetUserForToken(token.Id, user.Id);
            userId = user.Id;
        }
        else
        {
            userId = token.UserId;
        }

        // This should always get a value since we just created the user if it was null, and it is a foreign key.
        var userInfo = await userStore.GetUserInfo(userId)
            ?? throw new Exception("User not found");

        var lastSync = userInfo.LastSync ?? DateTime.MinValue;
        // TODO: handle more than 50 recently played tracks since last sync
        // NOTE: This is not really a problem since this will run every x minutes anyway and get the next 50 each time.
        var recentlyPlayedResponse = await api.Player.GetRecentlyPlayed(new PlayerRecentlyPlayedRequest
        {
            Limit = 50,
            After = lastSync.Millisecond,
        });
        if (recentlyPlayedResponse.Items?.Count is 0 or null)
        {
            // No recently played tracks since last sync. Nothing to do.
            continue;
        }

        var recentlyPlayed = recentlyPlayedResponse.Items.Select(track => new RecentlyPlayed(userId, track.Track.Id, track.PlayedAt));
        await recentlyPlayedStore.AddRecentlyPlayed(recentlyPlayed);

        await userStore.UpdateLastSync(userId, recentlyPlayed.Max(track => track.PlayedAt));
    }
    catch (APIUnauthorizedException)
    {
        // Token may have expired in the time between checking and using it.
        // TODO: Try to refresh token and retry once more.

        // Another possibility is that the token does not have the right scopes.
        // TODO: check scopes.
    }
}
