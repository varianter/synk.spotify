namespace Synk.Spotify;

public class RecentlyPlayedStore
{
    public Task<IEnumerable<RecentlyPlayed>> AddRecentlyPlayed(IEnumerable<RecentlyPlayed> recentlyPlayed)
    {
        throw new NotImplementedException();
    }
}

public record RecentlyPlayed(string UserId, string TrackId, DateTime PlayedAt);
