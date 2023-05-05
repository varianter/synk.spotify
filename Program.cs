using SpotifyAPI.Web;
using Synk.Spotify;

var tokenStore = new TokenStore();
var tokens = await tokenStore.GetTokens();

var userStore = new UserStore();
var recentlyPlayedStore = new RecentlyPlayedStore();

foreach (var token in tokens)
{
    // TODO: handle expired tokens
    var api = new SpotifyClient(token.AccessToken);
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
}
