using Npgsql;

namespace Synk.Spotify;

internal class RecentlyPlayedStore
{
    private readonly CockroachDbContext dbContext;

    internal RecentlyPlayedStore()
    {
        dbContext = new();
    }

    internal Task AddRecentlyPlayed(IEnumerable<RecentlyPlayed> recentlyPlayed)
    {
        using var connection = dbContext.GetConnection();

        var command = new NpgsqlCommand("INSERT INTO recentlyPlayed (userId, trackId, playedAt) VALUES (@userId, @trackId, @playedAt)", connection);
        command.Parameters.AddWithValue("userId", recentlyPlayed.Select(x => x.UserId));
        command.Parameters.AddWithValue("trackId", recentlyPlayed.Select(x => x.TrackId));
        command.Parameters.AddWithValue("playedAt", recentlyPlayed.Select(x => x.PlayedAt));

        return command.ExecuteNonQueryAsync();
    }
}

internal record RecentlyPlayed(string UserId, string TrackId, DateTime PlayedAt);
