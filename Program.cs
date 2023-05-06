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
        var refreshedToken = await tokenRefresher.RefreshTokenAsync(token.RefreshToken);
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
        var me = await api.UserProfile.Current();
        var userInfo = await userStore.GetUserInfo(me.Id);
        var lastSync = userInfo?.LastSync ?? DateTime.MinValue;
        // TODO: handle more than 50 recently played tracks since last sync
        var recentlyPlayedResponse = await api.Player.GetRecentlyPlayed(new PlayerRecentlyPlayedRequest
        {
            Limit = 50,
            After = lastSync.Millisecond,
        });
        if (recentlyPlayedResponse?.Items?.Count is 0 or null)
        {
            continue;
        }

        var recentlyPlayed = recentlyPlayedResponse.Items.Select(track => new RecentlyPlayed(me.Id, track.Track.Id, track.PlayedAt));
        await recentlyPlayedStore.AddRecentlyPlayed(recentlyPlayed);

        await userStore.UpdateLastSync(me.Id, recentlyPlayed.Max(track => track.PlayedAt));
    }
    catch (APIUnauthorizedException)
    {
        // Token may have expired in the time between checking and using it.
        // TODO: Try to refresh token and retry once more.

        // Another possibility is that the token does not have the right scopes.
        // TODO: check scopes.
    }
}
