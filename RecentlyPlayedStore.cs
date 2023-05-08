using System.Text;
using Npgsql;

namespace Synk.Spotify;

internal class RecentlyPlayedStore
{
    private readonly CockroachDbContext dbContext;

    internal RecentlyPlayedStore()
    {
        dbContext = new();
    }

    internal async Task AddRecentlyPlayed(IEnumerable<RecentlyPlayed> recentlyPlayed)
    {
        using var connection = dbContext.GetOpenConnection();
        var commandText = new StringBuilder("INSERT INTO recentlyPlayed (userId, trackId, playedAt) VALUES ");
        var itemIndex = 0;
        var first = true;
        var command = new NpgsqlCommand(null, connection);
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
