using System.Text;

namespace Synk.Spotify;

public class MusicStore
{
    private readonly CockroachDbContext dbContext;
    private readonly Logger logger = new($"{nameof(MusicStore)}: ");

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
        var commandText = $@"
            INSERT INTO tracks (id, name, album_id, duration {(track.preview_url is not null ? ", preview_url" : string.Empty)})
            VALUES (@id, @name, @albumId, @duration {(track.preview_url is not null ? ", @previewUrl" : string.Empty)})
        ";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", track.id);
        command.Parameters.AddWithValue("name", track.name);
        command.Parameters.AddWithValue("albumId", track.album.id);
        command.Parameters.AddWithValue("duration", track.duration_ms);
        if (track.preview_url is not null)
        {
            command.Parameters.AddWithValue("previewUrl", track.preview_url);
        }
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
        var commandText = "INSERT INTO albums (id, name, image_url, release_date) VALUES (@id, @name, @imageUrl, @releaseDate)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", album.id);
        command.Parameters.AddWithValue("name", album.name);
        command.Parameters.AddWithValue("imageUrl", album.BigImageUrl);
        command.Parameters.AddWithValue("releaseDate", album.release_date);
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

    internal Task<IEnumerable<SynkPlaylist>> GetNewPlaylists()
    {
        return GetSynkPlaylists(@"
            WHERE s.id IS NULL 
              AND p.is_current_top_list = true
              AND NOT EXISTS (
                SELECT 1
                FROM playlists p2 
                INNER JOIN spotify_playlists s2 ON p2.id = s2.playlist_id
                WHERE p2.group_id = p.group_id 
                  AND p2.name = p.name
              )
            ");
    }

    private async Task<IEnumerable<SynkPlaylist>> GetSynkPlaylists(string? filter, Dictionary<string, object>? parameters = null)
    {
        var commandText = $@"
            SELECT 
                p.id,
                g.id,
                g.group_id,
                p.name,
                p.is_current_top_list,
                s.id spotify_id
            FROM playlists p
            INNER JOIN groups g ON p.group_id = g.id
            LEFT JOIN spotify_playlists s ON p.id = s.playlist_id
            {filter}
        ";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        foreach (var (key, value) in parameters ?? new Dictionary<string, object>())
        {
            command.Parameters.AddWithValue(key, value);
        }
        using var reader = await command.ExecuteReaderAsync();
        var result = new List<SynkPlaylist>();
        while (await reader.ReadAsync())
        {
            result.Add(new SynkPlaylist(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.IsDBNull(5) ? null : reader.GetString(5)));
        }
        return result;
    }

    internal Task<IEnumerable<SynkPlaylist>> GetOutdatedPlaylists()
    {
        return GetSynkPlaylists("WHERE p.is_current_top_list = false AND s.id IS NOT NULL");
    }

    internal Task SetSpotifyIdForPlaylist(SynkPlaylist newPlaylist, string id)
    {
        var commandText = "UPDATE spotify_playlists SET id = @id where playlist_id = @playlistId";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("playlistId", newPlaylist.id);
        return command.ExecuteNonQueryAsync();
    }

    internal async Task<IEnumerable<PlaylistItem>> GetPlaylistItems(Guid playlist_id)
    {
        var commandText = @"
            SELECT track_id
            FROM playlist_items
            WHERE playlist_id = @playlistId
            ORDER BY score DESC
        ";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("playlistId", playlist_id);
        using var reader = await command.ExecuteReaderAsync();
        var result = new List<PlaylistItem>();
        while (reader.Read())
        {
            result.Add(new PlaylistItem(reader.GetString(0)));
        }
        return result;
    }

    internal async Task<SynkPlaylist?> GetCurrentPlaylist(SynkPlaylist outdatedPlaylist)
    {
        return (await GetSynkPlaylists("WHERE p.group_id = @groupId AND p.name = @name AND p.is_current_top_list = true",
            new Dictionary<string, object>()
            {
                ["@groupId"] = outdatedPlaylist.group_id,
                ["@name"] = outdatedPlaylist.name
            })).SingleOrDefault();
    }

    internal Task UpdatePlaylistIdForSpotifyPlaylist(string spotify_id, Guid id)
    {
        var commandText = "UPDATE spotify_playlists SET playlist_id = @playlistId WHERE id = @id";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("playlistId", id);
        command.Parameters.AddWithValue("id", spotify_id);
        return command.ExecuteNonQueryAsync();
    }

    internal Task MapNewSpotifyPlaylistToPlaylist(SynkPlaylist newPlaylist, string id)
    {
        var commandText = "INSERT INTO spotify_playlists (id, playlist_id) VALUES (@id, @playlistId)";
        using var command = dbContext.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("playlistId", newPlaylist.id);
        return command.ExecuteNonQueryAsync();
    }
}

#pragma warning disable IDE1006

public record PlaylistItem(string track_id);

public record SynkPlaylist(Guid id, Guid group_id, string group_path, string name, bool is_current_top_list, string? spotify_id);

#pragma warning restore IDE1006
