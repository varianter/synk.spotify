using System.Text;

namespace Synk.Spotify;

internal class MusicStore
{
    private readonly CockroachDbContext dbContext;

    internal MusicStore(CockroachDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    internal async Task StoreMissingTrackInfo(RecentlyPlayedResponse recentlyPlayedResponse)
    {
        foreach (var played in recentlyPlayedResponse.items)
        {
            foreach (var artist in played.track.artists)
            {
                if (await IsArtistMissing(artist.id))
                {
                    await CreateArtist(artist);
                }
            }
            if (await IsAlbumMissing(played.track.album.id))
            {
                await CreateAlbum(played.track.album);
            }
            if (await IsTrackMissing(played.track.id))
            {
                await CreateTrack(played.track);
                await LinkTrackToArtists(played.track);
            }
        }
    }

    internal async Task LinkTrackToArtists(Track track)
    {
        var commandText = new StringBuilder("INSERT INTO trackartists (trackid, artistid) VALUES ");
        var itemIndex = 0;
        var first = true;
        await using var command = dbContext.CreateCommand();
        foreach (var artist in track.artists)
        {
            if (first)
            {
                commandText.Append($"(@trackId, @artistId{itemIndex})");
                first = false;
            }
            else
            {
                commandText.Append($",(@trackId, @artistId{itemIndex})");
            }
            command.Parameters.AddWithValue($"trackId", track.id);
            command.Parameters.AddWithValue($"artistId{itemIndex}", artist.id);

            itemIndex++;
        }
        command.CommandText = commandText.ToString();
        await command.ExecuteNonQueryAsync();
    }

    internal async Task<bool> IsTrackMissing(string trackId)
    {
        var commandText = "SELECT EXISTS(SELECT 1 FROM tracks WHERE id = @trackId)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("trackId", trackId);
        var exists = await command.ExecuteScalarAsync() ?? throw new("Failed to check if track exists.");
        return !(bool)exists;
    }

    internal async Task CreateTrack(Track track)
    {
        var commandText = "INSERT INTO tracks (id, name, albumid, duration) VALUES (@id, @name, @albumId, @duration)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", track.id);
        command.Parameters.AddWithValue("name", track.name);
        command.Parameters.AddWithValue("albumId", track.album.id);
        command.Parameters.AddWithValue("duration", track.duration_ms);
        await command.ExecuteNonQueryAsync();
    }

    internal async Task<bool> IsAlbumMissing(string albumId)
    {
        var commandText = "SELECT EXISTS(SELECT 1 FROM albums WHERE id = @albumId)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("albumId", albumId);
        var exists = await command.ExecuteScalarAsync() ?? throw new("Failed to check if album exists.");
        return !(bool)exists;
    }

    internal async Task CreateAlbum(Album album)
    {
        var commandText = "INSERT INTO albums (id, name, imageurl) VALUES (@id, @name, @imageUrl)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", album.id);
        command.Parameters.AddWithValue("name", album.name);
        command.Parameters.AddWithValue("imageUrl", album.BigImageUrl);
        await command.ExecuteNonQueryAsync();
    }

    internal async Task CreateArtist(Artist artist)
    {
        var commandText = "INSERT INTO artists (id, name) VALUES (@id, @name)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", artist.id);
        command.Parameters.AddWithValue("name", artist.name);
        await command.ExecuteNonQueryAsync();
    }

    internal async Task<bool> IsArtistMissing(string artistId)
    {
        var commandText = "SELECT EXISTS(SELECT 1 FROM artists WHERE id = @artistId)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("artistId", artistId);
        var exists = await command.ExecuteScalarAsync() ?? throw new("Failed to check if artist exists.");
        return !(bool)exists;
    }
}
