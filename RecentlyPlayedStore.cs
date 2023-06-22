using System.Text;

namespace Synk.Spotify;

internal class RecentlyPlayedStore
{
    private readonly CockroachDbContext dbContext;

    internal RecentlyPlayedStore(CockroachDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    internal async Task AddRecentlyPlayed(IEnumerable<RecentlyPlayed> recentlyPlayed)
    {
        var commandText = new StringBuilder("INSERT INTO played_tracks (user_id, track_id, played_at) VALUES ");
        var itemIndex = 0;
        var first = true;
        await using var command = dbContext.CreateCommand();
        foreach (var item in recentlyPlayed)
        {
            if (first)
            {
                commandText.Append($"(@userId{itemIndex}, @trackId{itemIndex}, @playedAt{itemIndex})");
                first = false;
            }
            else
            {
                commandText.Append($",(@userId{itemIndex}, @trackId{itemIndex}, @playedAt{itemIndex})");
            }
            command.Parameters.AddWithValue($"userId{itemIndex}", item.UserId);
            command.Parameters.AddWithValue($"trackId{itemIndex}", item.TrackId);
            command.Parameters.AddWithValue($"playedAt{itemIndex}", item.PlayedAt);

            itemIndex++;
        }
        command.CommandText = commandText.ToString();
        await command.ExecuteNonQueryAsync();
    }
}

internal record RecentlyPlayed(string UserId, string TrackId, DateTime PlayedAt);
