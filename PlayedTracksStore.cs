using System.Text;

namespace Synk.Spotify;

internal class PlayedTracksStore
{
    private readonly CockroachDbContext dbContext;

    internal PlayedTracksStore(CockroachDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    internal async Task AddPlayedTrack(IEnumerable<PlayedTrack> recentlyPlayed)
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

internal record PlayedTrack(string UserId, string TrackId, DateTime PlayedAt);
