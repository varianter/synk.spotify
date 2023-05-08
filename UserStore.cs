using Npgsql;

namespace Synk.Spotify;

internal class UserStore
{
    private readonly CockroachDbContext dbContext;

    internal UserStore()
    {
        dbContext = new();
    }

    internal async Task<UserInfo?> GetUserInfo(string userId)
    {
        using var connection = dbContext.GetOpenConnection();

        var command = new NpgsqlCommand("SELECT id, lastSync FROM users WHERE id = @userId", connection);
        command.Parameters.AddWithValue("userId", userId);

        using var reader = await command.ExecuteReaderAsync();
        if (!reader.HasRows)
        {
            return null;
        }

        await reader.ReadAsync();
        return new UserInfo(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetDateTime(1));
    }

    internal async Task CreateUser(string id)
    {
        using var connection = dbContext.GetOpenConnection();

        var command = new NpgsqlCommand("INSERT INTO users (id) VALUES (@id)", connection);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync();
    }

    internal async Task UpdateLastSync(string id, DateTime lastSyncTime)
    {
        using var connection = dbContext.GetOpenConnection();

        var command = new NpgsqlCommand("UPDATE users SET lastSync = @lastSync WHERE id = @id", connection);
        command.Parameters.AddWithValue("lastSync", lastSyncTime);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync();
    }
}

internal record UserInfo(string UserId, DateTime? LastSync);
