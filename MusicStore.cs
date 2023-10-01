using System.Text;

namespace Synk.Spotify;

public class MusicStore
{
    private readonly CockroachDbContext dbContext;

    public MusicStore(CockroachDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task UpdateArtistImage(string artistId, string imageUrl)
    {
        var commandText = "UPDATE artists SET image_url = @imageUrl WHERE id = @artistId";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("imageUrl", imageUrl);
        command.Parameters.AddWithValue("artistId", artistId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<Artist>> GetArtistsWithoutImages()
    {
        var commandText = "SELECT id, name FROM artists WHERE image_url IS NULL";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync();
        var artists = new List<Artist>();
        while (await reader.ReadAsync())
        {
            artists.Add(new Artist(
                id: reader.GetString(0),
                name: reader.GetString(1)
            ));
        }
        return artists;
    }

    public async Task StoreMissingTrackInfo(RecentlyPlayedResponse recentlyPlayedResponse)
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

    public async Task LinkTrackToArtists(Track track)
    {
        var commandText = new StringBuilder("INSERT INTO track_artists (track_id, artist_id, artist_order) VALUES ");
        var itemIndex = 0;
        var first = true;
        await using var command = dbContext.CreateCommand();
        foreach (var artist in track.artists)
        {
            if (first)
            {
                commandText.Append($"(@trackId, @artistId{itemIndex}, {itemIndex})");
                first = false;
            }
            else
            {
                commandText.Append($",(@trackId, @artistId{itemIndex}, {itemIndex})");
            }
            command.Parameters.AddWithValue($"trackId", track.id);
            command.Parameters.AddWithValue($"artistId{itemIndex}", artist.id);

            itemIndex++;
        }
        command.CommandText = commandText.ToString();
        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> IsTrackMissing(string trackId)
    {
        var commandText = "SELECT EXISTS(SELECT 1 FROM tracks WHERE id = @trackId)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("trackId", trackId);
        var exists = await command.ExecuteScalarAsync() ?? throw new("Failed to check if track exists.");
        return !(bool)exists;
    }

    public async Task CreateTrack(Track track)
    {
        var commandText = "INSERT INTO tracks (id, name, album_id, duration) VALUES (@id, @name, @albumId, @duration)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", track.id);
        command.Parameters.AddWithValue("name", track.name);
        command.Parameters.AddWithValue("albumId", track.album.id);
        command.Parameters.AddWithValue("duration", track.duration_ms);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> IsAlbumMissing(string albumId)
    {
        var commandText = "SELECT EXISTS(SELECT 1 FROM albums WHERE id = @albumId)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("albumId", albumId);
        var exists = await command.ExecuteScalarAsync() ?? throw new("Failed to check if album exists.");
        return !(bool)exists;
    }

    public async Task CreateAlbum(Album album)
    {
        var commandText = "INSERT INTO albums (id, name, image_url) VALUES (@id, @name, @imageUrl)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", album.id);
        command.Parameters.AddWithValue("name", album.name);
        command.Parameters.AddWithValue("imageUrl", album.BigImageUrl);
        await command.ExecuteNonQueryAsync();
    }

    public async Task CreateArtist(Artist artist)
    {
        var commandText = "INSERT INTO artists (id, name) VALUES (@id, @name)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", artist.id);
        command.Parameters.AddWithValue("name", artist.name);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> IsArtistMissing(string artistId)
    {
        var commandText = "SELECT EXISTS(SELECT 1 FROM artists WHERE id = @artistId)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("artistId", artistId);
        var exists = await command.ExecuteScalarAsync() ?? throw new("Failed to check if artist exists.");
        return !(bool)exists;
    }

    public async Task<IEnumerable<string>> GetTrackIdsWithoutArtists()
    {
        var commandText = "SELECT id, name, album_id, duration FROM tracks WHERE id NOT IN (SELECT track_id FROM track_artists)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync();
        var trackIds = new List<string>();
        while (await reader.ReadAsync())
        {
            trackIds.Add(reader.GetString(0));
        }
        return trackIds;
    }

    internal async Task<IEnumerable<string>> GetTrackIdsWithoutPreviewUrl()
    {
        var commandText = "SELECT id FROM tracks WHERE preview_url IS NULL";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync();
        var trackIds = new List<string>();
        while (await reader.ReadAsync())
        {
            trackIds.Add(reader.GetString(0));
        }
        return trackIds;
    }

    internal Task UpdateTrackPreviewUrlAndAlbumReleaseDate(Track spotifyTrack)
    {
        var commandText = $@"
        {(spotifyTrack.preview_url is not null ? "UPDATE tracks SET preview_url = @previewUrl WHERE id = @trackId;" : "")}
        UPDATE albums SET release_date = @albumReleaseDate WHERE id = @albumId AND release_date IS NULL;
        ";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        if (spotifyTrack.preview_url is not null)
        {
            command.Parameters.AddWithValue("previewUrl", spotifyTrack.preview_url);
        }
        command.Parameters.AddWithValue("albumReleaseDate", spotifyTrack.album.release_date);
        command.Parameters.AddWithValue("trackId", spotifyTrack.id);
        command.Parameters.AddWithValue("albumId", spotifyTrack.album.id);
        return command.ExecuteNonQueryAsync();
    }
}
